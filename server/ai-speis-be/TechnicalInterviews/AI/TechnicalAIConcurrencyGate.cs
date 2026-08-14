using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public interface ITechnicalAIConcurrencyGate
    {
        ValueTask<IAsyncDisposable> EnterAsync(CancellationToken cancellationToken);
    }

    public sealed class TechnicalAIConcurrencyGate : ITechnicalAIConcurrencyGate, IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public TechnicalAIConcurrencyGate(TechnicalInterviewOptions options)
        {
            _semaphore = new SemaphoreSlim(
                options.GlobalConcurrencyLimit,
                options.GlobalConcurrencyLimit);
        }

        public async ValueTask<IAsyncDisposable> EnterAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new Lease(_semaphore);
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }

        private sealed class Lease : IAsyncDisposable
        {
            private SemaphoreSlim? _semaphore;

            public Lease(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public ValueTask DisposeAsync()
            {
                Interlocked.Exchange(ref _semaphore, null)?.Release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
