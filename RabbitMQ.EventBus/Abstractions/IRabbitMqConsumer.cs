using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Abstractions
{
    public interface IRabbitMqConsumer
    {
        //Task StartAsync(ConsumerOptions context, CancellationToken cancellationToken);

        Task StopAsync(CancellationToken cancellationToken);

        Task StartConsumerAsync<T>(
            ConsumerOptions options,
            Func<MqMessage<T>, CancellationToken, Task<bool>> handler,
            CancellationToken cancellationToken);
    }
}
