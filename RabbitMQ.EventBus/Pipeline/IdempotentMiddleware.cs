using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Pipeline
{
    public class IdempotentMiddleware<T> : IConsumerMiddleware<T>
    {
        private readonly HashSet<string> _processedIds = new();

        public Task<bool> InvokeAsync(MqMessage<T> message, CancellationToken ct, Func<MqMessage<T>, CancellationToken, Task<bool>> next)
        {
            if (_processedIds.Contains(message.MessageId))
                return Task.FromResult(true); // Already processed

            return next(message, ct).ContinueWith(t =>
            {
                if (t.Result)
                    _processedIds.Add(message.MessageId);
                return t.Result;
            });
        }
    }
}
