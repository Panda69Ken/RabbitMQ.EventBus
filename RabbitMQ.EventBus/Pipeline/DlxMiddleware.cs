using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Pipeline
{
    public class DlxMiddleware<T> : IConsumerMiddleware<T>
    {
        private readonly ConsumerOptions _options;
        private readonly Func<MqMessage<T>, ConsumerOptions, Task> _publishDlx;

        public DlxMiddleware(ConsumerOptions options, Func<MqMessage<T>, ConsumerOptions, Task> publishDlx)
        {
            _options = options;
            _publishDlx = publishDlx;
        }

        public async Task<bool> InvokeAsync(MqMessage<T> message, CancellationToken ct, Func<MqMessage<T>, CancellationToken, Task<bool>> next)
        {
            var result = await next(message, ct);
            if (!result && !string.IsNullOrWhiteSpace(_options.DeadLetterExchange))
            {
                var maxRetry = _options.RetryLevels?.Count ?? 0;
                if (message.RetryCount == maxRetry)
                {
                    await _publishDlx(message, _options);
                }
            }
            return result;
        }
    }
}
