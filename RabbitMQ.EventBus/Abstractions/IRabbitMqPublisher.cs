namespace RabbitMQ.EventBus.Abstractions
{
    public interface IRabbitMqPublisher
    {
        Task PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            CancellationToken cancellationToken = default);
    }
}
