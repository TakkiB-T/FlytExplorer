using FlytExplorer.Core.Models;

namespace FlytExplorer.Core.Interfaces;

public interface ISearchProvider
{
    Task<SearchPage> SearchAsync(SearchRequest request, CancellationToken cancellationToken);
    Task<SearchHealth> GetHealthAsync(CancellationToken cancellationToken);
}
