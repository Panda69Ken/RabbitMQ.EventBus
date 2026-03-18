using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Pipeline
{
    public interface IConsumerMiddleware<T>
    {
        Task<bool> InvokeAsync(MqMessage<T> message, CancellationToken ct, Func<MqMessage<T>, CancellationToken, Task<bool>> next);
    }
}
