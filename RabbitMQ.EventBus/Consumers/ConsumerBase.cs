using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.EventBus.Abstractions;
using RabbitMQ.EventBus.Internal;
using RabbitMQ.EventBus.Model;
using RabbitMQ.EventBus.Pipeline;
using System.Text;

namespace RabbitMQ.EventBus.Engine
{
    public abstract class ConsumerBase<T> : IAsyncBasicConsumer, IConsumerControl
    {
        protected readonly ILogger _logger;
        protected readonly IChannel _channel;
        protected readonly ConsumerOptions _options;

        private volatile bool _stopping;
        private readonly AsyncInflightCounter _inflight = new();
        private readonly Func<MqMessage<T>, CancellationToken, Task<bool>> _pipeline;

        public ConsumerBase(
            IChannel channel,
            ILogger logger,
            ConsumerOptions options,
            ConsumerPipelineBuilder<T> pipelineBuilder)
        {
            _logger = logger;
            _channel = channel;
            _options = options;
            _pipeline = pipelineBuilder.Build(HandleMessageAsync) ?? HandleMessageAsync;
        }

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

                MqMessage<T>? message = null;
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(_options.HandlerTimeout);

                    message = JsonConvert.DeserializeObject<MqMessage<T>>(Encoding.UTF8.GetString(body.Span));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "消息反序列化异常");
                }

                bool success = false;
                if (message != null)
                {
                    success = await _pipeline(message, cancellationToken);
                }

                if (_stopping)
                {
                    _logger.LogWarning($"[MQ] stopping=true → requeue deliveryTag={deliveryTag}");
                    await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true, cancellationToken);
                }
                else
                {
                    await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
                }
            }
            finally
            {
                _inflight.Decrement();
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
