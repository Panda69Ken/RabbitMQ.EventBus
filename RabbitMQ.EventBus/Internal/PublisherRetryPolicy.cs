using Microsoft.Extensions.Logging;
using Polly;

namespace RabbitMQ.EventBus.Internal
{
    internal static class PublisherRetryPolicy
    {
        /// <summary>
        /// 构建简单的异步重试策略，采用指数退避+抖动
        /// </summary>
        public static IAsyncPolicy Create(
            int retryCount = 3,
            TimeSpan? baseDelay = null,
            ILogger? logger = null)
        {
            baseDelay ??= TimeSpan.FromSeconds(1);
            var jitterer = new Random();

            return Policy.Handle<Exception>().WaitAndRetryAsync(
                retryCount,
                attempt =>
                {
                    // 指数退避 + 抖动
                    var exponential = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    var jitter = TimeSpan.FromMilliseconds(jitterer.Next(0, 300));
                    return baseDelay.Value + exponential + jitter;
                },
                onRetry: (ex, timespan, retryAttempt, context) =>
                {
                    logger?.LogWarning($"[MQ] Publish retry {retryAttempt} after {timespan.TotalSeconds}s due to: {ex.Message}");
                });
        }
    }
}