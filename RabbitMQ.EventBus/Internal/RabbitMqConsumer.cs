using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.EventBus.Abstractions;
using RabbitMQ.EventBus.Engine;
using RabbitMQ.EventBus.Model;
using RabbitMQ.EventBus.Pipeline;
using System.Collections.Concurrent;

namespace RabbitMQ.EventBus.Internal
{
    public class RabbitMqConsumer(IConnection connection,
            ILogger<RabbitMqConsumer> logger) : IRabbitMqConsumer
    {
        protected readonly IConnection _connection = connection;
        protected readonly ILogger<RabbitMqConsumer> _logger = logger;

        private readonly ConcurrentDictionary<string, ConsumerRuntime> _consumers = new();

        //public async Task StartAsync(ConsumerOptions options, CancellationToken cancellationToken = default)
        //{
        //    _channel ??= await _connection.CreateChannelAsync();

        //    //初始化 Exchange Queue DLX（必须在 consume 之前）
        //    await EnsureTopologyAsync(_channel, options, cancellationToken);
        //}

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[MQ] Stopping consumer");

            //此写法就成了串行停机
            //foreach (var (_, runtime) in _consumers)
            //{
            //    runtime.Consumer.MarkStopping();
            //    await runtime.Channel.BasicCancelAsync(runtime.ConsumerTag, cancellationToken: cancellationToken);
            //    await runtime.Consumer.DrainAsync(cancellationToken);
            //    await runtime.DisposeAsync();
            //}

            var runtimes = _consumers.Values.ToList();
            //标记停止
            foreach (var runtime in runtimes)
            {
                runtime.Consumer.MarkStopping();
            }
            //取消订阅（停止新消息）
            await Task.WhenAll(runtimes.Select(r => r.Channel.BasicCancelAsync(r.ConsumerTag, cancellationToken: cancellationToken)));
            //等待处理完成
            await Task.WhenAll(runtimes.Select(r => r.Consumer.DrainAsync(cancellationToken)));
            //关闭资源
            await Task.WhenAll(runtimes.Select(async r => await r.DisposeAsync()));

            _consumers.Clear();
            _logger.LogInformation("[MQ] Consumer stopped");
        }


        public async Task StartConsumerAsync<T>(ConsumerOptions options,
            Func<MqMessage<T>, CancellationToken, Task<bool>> handler,
            CancellationToken cancellationToken = default)
        {
            var channel = await _connection.CreateChannelAsync();

            await EnsureTopologyAsync(channel, options, cancellationToken);

            await channel.BasicQosAsync(0, options.PrefetchCount, false, cancellationToken);

            var pipelineBuilder = new ConsumerPipelineBuilder<T>()
                .Use(new ConcurrencyMiddleware<T>(options.PrefetchCount))
                .Use(new IdempotentMiddleware<T>())
                .Use(new RetryStormProtectionMiddleware<T>(1000))
                .Use(new RetryMiddleware<T>(options, async (msg, opt) => await PublishRetryAsync(channel, msg, opt, cancellationToken)))
                .Use(new DlxMiddleware<T>(options, async (msg, opt) => await PublishDlxAsync(channel, msg, opt, cancellationToken)))
                .Use(new GracefulShutdownMiddleware<T>());

            var consumer = new DelegateConsumer<T>(channel, _logger, options, handler, pipelineBuilder);

            var consumerTag = await channel.BasicConsumeAsync(
                queue: options.Queue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);

            _consumers.TryAdd($"{options.Queue}:{Guid.NewGuid():N}", new ConsumerRuntime(consumerTag, channel, consumer));
        }

        private async Task PublishRetryAsync<T>(IChannel channel, MqMessage<T> message, ConsumerOptions options, CancellationToken ct)
        {
            var retryLevel = options.RetryLevels.FirstOrDefault(lv => lv.RetryCount == message.RetryCount);
            if (retryLevel == null) return;

            var retryQueue = !string.IsNullOrWhiteSpace(retryLevel.QueueName) ? retryLevel.QueueName : $"{options.Queue}.retry.{retryLevel.RetryCount}";
            var retryExchange = options.RetryExchange ?? options.Exchange;
            var delay = retryLevel.Delay;

            var props = new BasicProperties
            {
                Persistent = true,
                Headers = new Dictionary<string, object?>()
            };

            props.Headers["x-retry"] = true;
            props.Headers["x-retry-count"] = message.RetryCount;
            props.Headers["x-retry-at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            props.Headers["x-original-exchange"] = options.Exchange;
            props.Headers["x-original-routing-key"] = message.EventType;
            props.Headers["x-original-queue"] = options.Queue;

            // 设置延迟（依赖于 x-delayed-message 插件或 TTL+DLX 方案）
            if (delay > TimeSpan.Zero)
            {
                props.Headers["x-delay"] = (int)delay.TotalMilliseconds;
            }

            var json = JsonConvert.SerializeObject(message);

            await channel.BasicPublishAsync(
                exchange: retryExchange,
                routingKey: retryQueue,
                mandatory: false,
                basicProperties: props,
                body: System.Text.Encoding.UTF8.GetBytes(json),
                cancellationToken: ct);
        }

        private async Task PublishDlxAsync<T>(IChannel channel, MqMessage<T> message, ConsumerOptions options, CancellationToken ct)
        {
            var json = JsonConvert.SerializeObject(message);

            if (!string.IsNullOrWhiteSpace(options.DeadLetterExchange))
            {
                var dlxExchange = options.DeadLetterExchange;
                var dlxQueue = !string.IsNullOrWhiteSpace(options.DeadLetterQueue) ? options.DeadLetterQueue : $"{options.Queue}.dlq";

                var props = new BasicProperties
                {
                    Persistent = true,
                    Headers = new Dictionary<string, object?>()
                };

                //DLQ 标记
                props.Headers["x-dead"] = true; //是否死信
                props.Headers["x-dead-at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); //进入 DLQ 时间

                props.Headers["x-original-exchange"] = options.Exchange;    //原 exchange
                props.Headers["x-original-routing-key"] = message.EventType;   //目标Queue
                props.Headers["x-original-queue"] = options.Queue;  //原队列

                // 修正：routingKey 应使用原始业务 routingKey，而不是 dlxQueue
                var routingKey = message.EventType; // 或根据实际业务场景选择正确的 routingKey

                await channel.BasicPublishAsync(
                    exchange: dlxExchange,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: System.Text.Encoding.UTF8.GetBytes(json),
                    cancellationToken: ct);

                _logger.LogWarning($"[MQ] 消息已发送到死信交换机 '{options.DeadLetterExchange}',EventType:{message.EventType},body:{json}");
            }
            else
            {
                _logger.LogWarning($"[MQ] 未配置死信交换机,消息将被丢弃,EventType:{message.EventType},body:{json}");
            }
        }

        /// <summary>
        /// 幂等，ExchangeDeclare / QueueDeclare 天生幂等
        /// 自动 DLQ 命名，routingKey 复用，DLQ 能知道“原来是哪类消息”
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="options"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task EnsureTopologyAsync(
            IChannel channel,
            ConsumerOptions options,
            CancellationToken ct)
        {
            // 主 Exchange
            await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

            // 主 Queue
            await channel.QueueDeclareAsync(queue: options.Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

            // Bind routingKey
            foreach (var key in options.RoutingKeys)
            {
                await channel.QueueBindAsync(queue: options.Queue, exchange: options.Exchange, routingKey: key, cancellationToken: ct);
            }

            // DLQ
            if (!string.IsNullOrWhiteSpace(options.DeadLetterExchange))
            {
                var dlxQueue = !string.IsNullOrWhiteSpace(options.DeadLetterQueue) ? options.DeadLetterQueue : $"{options.Queue}.dlq";

                //DLX Exchange
                await channel.ExchangeDeclareAsync(options.DeadLetterExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

                //DLX Queue
                await channel.QueueDeclareAsync(dlxQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

                // 恢复DLQ队列与DLX交换机的绑定，确保消息能正确路由到DLQ
                foreach (var routingKey in options.RoutingKeys)
                {
                    await channel.QueueBindAsync(queue: dlxQueue, exchange: options.DeadLetterExchange, routingKey: routingKey, cancellationToken: ct);
                }
            }

            // Retry
            if (options.RetryLevels.Count > 0)
            {
                //Retry Exchange
                if (!string.IsNullOrWhiteSpace(options.RetryExchange))
                {
                    await channel.ExchangeDeclareAsync(options.RetryExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
                }

                //Retry Queue
                foreach (var retryLevel in options.RetryLevels)
                {
                    var retryExchange = options.RetryExchange ?? options.Exchange;

                    var retryQueue = !string.IsNullOrWhiteSpace(retryLevel.QueueName) ? retryLevel.QueueName : $"{options.Queue}.retry.{retryLevel.RetryCount}";

                    await channel.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

                    await channel.QueueBindAsync(queue: retryQueue, exchange: retryExchange, routingKey: retryQueue, cancellationToken: ct);
                }

            }

        }

    }
}
