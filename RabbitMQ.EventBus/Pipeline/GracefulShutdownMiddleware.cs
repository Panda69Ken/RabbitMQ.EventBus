using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Pipeline
{
    public class GracefulShutdownMiddleware<T> : IConsumerMiddleware<T>
    {
        private volatile bool _stopping = false;

        public void MarkStopping() => _stopping = true;

        public Task<bool> InvokeAsync(MqMessage<T> message, CancellationToken ct, Func<MqMessage<T>, CancellationToken, Task<bool>> next)
        {
            if (_stopping)
                return Task.FromResult(false);
            return next(message, ct);
        }
    }
}
