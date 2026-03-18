using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Pipeline
{
    public class ConcurrencyMiddleware<T> : IConsumerMiddleware<T>
    {
        private readonly SemaphoreSlim _semaphore;

        public ConcurrencyMiddleware(int maxConcurrency)
        {
            _semaphore = new SemaphoreSlim(maxConcurrency);
        }

        public async Task<bool> InvokeAsync(MqMessage<T> message, CancellationToken ct, Func<MqMessage<T>, CancellationToken, Task<bool>> next)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                return await next(message, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
