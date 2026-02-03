using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core.Services.Search
{
    /// <summary>
    /// Adaptador que expone el rate limiter existente (MainForm.WaitForRateLimitAsync)
    /// a través del contrato ISearchRateLimiter.
    /// </summary>
    public sealed class SearchRateLimiterAdapter : ISearchRateLimiter
    {
        private readonly Func<CancellationToken, Task> waitAsync;
        private readonly Action? onSuccess;
        private readonly Action? onFailure;

        public SearchRateLimiterAdapter(
            Func<CancellationToken, Task> waitAsync,
            Action? onSuccess = null,
            Action? onFailure = null)
        {
            this.waitAsync = waitAsync ?? throw new ArgumentNullException(nameof(waitAsync));
            this.onSuccess = onSuccess;
            this.onFailure = onFailure;
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return waitAsync(cancellationToken);
        }

        public void RecordSuccess()
        {
            onSuccess?.Invoke();
        }

        public void RecordFailure()
        {
            onFailure?.Invoke();
        }
    }
}
