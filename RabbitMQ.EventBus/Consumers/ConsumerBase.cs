using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.EventBus.Abstractions;
using RabbitMQ.EventBus.Internal;
using RabbitMQ.EventBus.Model;
using System.Text;

namespace RabbitMQ.EventBus.Engine
{
    public abstract class ConsumerBase<T>(
        IChannel channel,
        ILogger logger,
        ConsumerOptions options) : IAsyncBasicConsumer, IConsumerControl
    {
        protected readonly ILogger _logger = logger;
        protected readonly IChannel _channel = channel;
        protected readonly ConsumerOptions _options = options;

        private volatile bool _stopping;
        private readonly AsyncInflightCounter _inflight = new();

        public void MarkStopping() => _stopping = true;

        public Task DrainAsync(CancellationToken ct)
        {
            return _inflight.WaitAsync(ct);
        }

        public IChannel Channel => _channel;

        public async Task HandleBasicDeliverAsync(
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IReadOnlyBasicProperties properties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            _inflight.Increment();

            if (body.Length == 0)
            {
                await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                return;
            }

            try
            {
                //如果正在停机，直接 requeue
                if (_stopping)
                {
                    _logger.LogWarning($"[MQ] stopping=true → requeue deliveryTag={deliveryTag}");
                    await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true, cancellationToken);
                    return;
                }

                bool success = false;
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(_options.HandlerTimeout);

                    var message = JsonConvert.DeserializeObject<MqMessage<T>>(Encoding.UTF8.GetString(body.Span));

                    success = await HandleMessageAsync(message, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("消息处理超时");
                    success = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "消费异常");
                    success = false;
                }

                if (_stopping)
                {
                    _logger.LogWarning($"[MQ] stopping=true → requeue deliveryTag={deliveryTag}");
                    await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true, cancellationToken);
                }
                else
                {
                    // 成功 Ack
                    if (success)
                    {
                        await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                    }
                    else
                    {
                        // 失败 → DLX + Ack
                        await PublishToDlxAsync(_options, routingKey, body, cancellationToken);
                        await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                    }
                }
            }
            finally
            {
                _inflight.Decrement();
            }
        }


        private async Task PublishToDlxAsync(ConsumerOptions options,
            string routingKey,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken)
        {
            var json = Encoding.UTF8.GetString(body.Span);

            // 失败 → 发送到 DLX      
            if (!string.IsNullOrWhiteSpace(_options.DeadLetterExchange))
            {
                var props = new BasicProperties
                {
                    Persistent = true,
                    Headers = new Dictionary<string, object?>()
                };

                //DLQ 标记
                props.Headers["x-dead"] = true; //是否死信
                props.Headers["x-dead-at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); //进入 DLQ 时间

                props.Headers["x-original-exchange"] = options.Exchange;    //原 exchange
                props.Headers["x-original-routing-key"] = routingKey;   //原 routingKey
                props.Headers["x-original-queue"] = options.Queue;  //原队列

                await _channel.BasicPublishAsync(
                    exchange: options.DeadLetterExchange,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body.ToArray(),
                    cancellationToken: cancellationToken);

                _logger.LogWarning($"[MQ] 消息已发送到死信交换机 '{_options.DeadLetterExchange}',routingKey:{routingKey},body:{json}");
            }
            else
            {
                _logger.LogWarning($"[MQ] 未配置死信交换机,消息将被丢弃,routingKey:{routingKey},body:{json}");
            }
        }

        protected abstract Task<bool> HandleMessageAsync(MqMessage<T> message, CancellationToken cancellationToken);

        // 下面这些是协议回调，通常空实现即可
        public Task HandleBasicCancelAsync(string consumerTag, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task HandleBasicCancelOkAsync(string consumerTag, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task HandleBasicConsumeOkAsync(string consumerTag, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
            => Task.CompletedTask;

    }

}
