using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Pipeline
{
    public class RetryStormProtectionMiddleware<T> : IConsumerMiddleware<T>
    {
        private readonly int _maxRetryPerMinute;
        private int _retryCount = 0;
        private DateTime _windowStart = DateTime.UtcNow;

        public RetryStormProtectionMiddleware(int maxRetryPerMinute)
        {
            _maxRetryPerMinute = maxRetryPerMinute;
        }

        public Task<bool> InvokeAsync(MqMessage<T> message, CancellationToken ct, Func<MqMessage<T>, CancellationToken, Task<bool>> next)
        {
            var now = DateTime.UtcNow;
            if ((now - _windowStart).TotalMinutes >= 1)
            {
                _windowStart = now;
                _retryCount = 0;
            }
            if (message.RetryCount > 0)
            {
                _retryCount++;
                if (_retryCount > _maxRetryPerMinute)
                    return Task.FromResult(false); // block storm
            }
            return next(message, ct);
        }
    }
}
