namespace RabbitMQ.EventBus.Abstractions
{
    public interface IConsumerControl
    {
        void MarkStopping();

        Task DrainAsync(CancellationToken cancellationToken);
    }
}
