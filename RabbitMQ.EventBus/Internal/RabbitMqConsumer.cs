using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.EventBus.Abstractions;
using RabbitMQ.EventBus.Engine;
using RabbitMQ.EventBus.Model;
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

            //初始化 Exchange Queue DLX（必须在 consume 之前）
            await EnsureTopologyAsync(channel, options, cancellationToken);

            //QoS
            await channel.BasicQosAsync(0, options.PrefetchCount, false, cancellationToken);

            //Consumer
            var consumer = new DelegateConsumer<T>(channel, _logger, options, handler);

            var consumerTag = await channel.BasicConsumeAsync(
                queue: options.Queue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);

            _consumers.TryAdd($"{options.Queue}:{Guid.NewGuid():N}", new ConsumerRuntime(consumerTag, channel, consumer));
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

                //Bind（复用原 routingKey）
                foreach (var routingKey in options.RoutingKeys)
                {
                    await channel.QueueBindAsync(queue: dlxQueue, exchange: options.DeadLetterExchange, routingKey: routingKey, cancellationToken: ct);
                }
            }
        }

    }
}
