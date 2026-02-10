namespace RabbitMQ.EventBus.Internal
{
    internal class AsyncInflightCounter
    {
        private int _count;
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Current => Volatile.Read(ref _count);

        public void Increment()
        {
            Interlocked.Increment(ref _count);
        }

        public void Decrement()
        {
            if (Interlocked.Decrement(ref _count) == 0)
            {
                _tcs.TrySetResult();
            }
        }

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            if (Current == 0)
                return Task.CompletedTask;

            if (!cancellationToken.CanBeCanceled)
                return _tcs.Task;

            return WaitWithCancellationAsync(cancellationToken);
        }

        private async Task WaitWithCancellationAsync(CancellationToken ct)
        {
            using var reg = ct.Register(() => _tcs.TrySetCanceled(ct));

            await _tcs.Task.ConfigureAwait(false);
        }

    }
}
