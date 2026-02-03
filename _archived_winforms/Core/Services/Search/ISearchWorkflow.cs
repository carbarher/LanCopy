using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core.Services.Search
{
    public interface ISearchWorkflow
    {
        Task<SearchWorkflowResult> ExecuteAsync(SearchWorkflowRequest request, CancellationToken cancellationToken);
    }
}
