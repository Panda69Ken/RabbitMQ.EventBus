using RabbitMQ.Client;
using RabbitMQ.EventBus.Abstractions;

namespace RabbitMQ.EventBus.Engine
{
    internal sealed class ConsumerRuntime(
        string consumerTag,
        IChannel channel,
        IConsumerControl consumer) : IAsyncDisposable
    {
        public string ConsumerTag { get; } = consumerTag;
        public IChannel Channel { get; } = channel;
        public IConsumerControl Consumer { get; } = consumer;

        public async ValueTask DisposeAsync()
        {
            await Channel.CloseAsync();
        }
    }
}
