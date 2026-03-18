using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Pipeline
{
    public class RetryMiddleware<T> : IConsumerMiddleware<T>
    {
        private readonly ConsumerOptions _options;
        private readonly Func<MqMessage<T>, ConsumerOptions, Task> _publishRetry;

        public RetryMiddleware(ConsumerOptions options, Func<MqMessage<T>, ConsumerOptions, Task> publishRetry)
        {
            _options = options;
            _publishRetry = publishRetry;
        }

        public async Task<bool> InvokeAsync(MqMessage<T> message, CancellationToken ct, Func<MqMessage<T>, CancellationToken, Task<bool>> next)
        {
            var result = await next(message, ct);
            if (!result)
            {
                message.RetryCount++;
                var maxRetry = _options.RetryLevels?.Count ?? 0;
                if (message.RetryCount <= maxRetry)
                {
                    await _publishRetry(message, _options);
                    return false;
                }
            }
            return result;
        }
    }
}
