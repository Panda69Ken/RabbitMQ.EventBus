using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.EventBus.Model;
using RabbitMQ.EventBus.Pipeline;

namespace RabbitMQ.EventBus.Engine
{
    public class DelegateConsumer<T>(
        IChannel channel,
        ILogger logger,
        ConsumerOptions options,
        Func<MqMessage<T>, CancellationToken, Task<bool>> handler,
        ConsumerPipelineBuilder<T> pipelineBuilder)
        : ConsumerBase<T>(channel, logger, options, pipelineBuilder)
    {
        private readonly Func<MqMessage<T>, CancellationToken, Task<bool>> _handler = handler;

        protected override Task<bool> HandleMessageAsync(MqMessage<T> message, CancellationToken ct) => _handler(message, ct);
    }
}
