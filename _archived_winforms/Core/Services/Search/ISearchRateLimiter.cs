using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core.Services.Search
{
    public interface ISearchRateLimiter
    {
        Task WaitAsync(CancellationToken cancellationToken);
        void RecordSuccess();
        void RecordFailure();
    }
}
