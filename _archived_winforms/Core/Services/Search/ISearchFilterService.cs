using Soulseek;

namespace SlskDown.Core.Services.Search
{
    public interface ISearchFilterService
    {
        SearchFilterResult FilterResponse(Soulseek.SearchResponse response, SearchFilterContext context);
    }
}
