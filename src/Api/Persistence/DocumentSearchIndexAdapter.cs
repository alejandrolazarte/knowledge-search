using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch;

internal sealed class DocumentSearchIndexAdapter(IDbService dbService) : IDocumentSearchIndex
{
    public Result<IReadOnlyList<SearchResult>> Search(
        string query,
        int limit,
        SearchMode modes,
        IReadOnlyList<string>? roots)
    {
        return Result.Success(dbService.Search(query, limit, modes, roots));
    }

    public IndexResult IndexDirectories() => dbService.IndexDirectories();

    public bool IsPathAllowed(string fullPath) => dbService.IsPathAllowed(fullPath);

    public IReadOnlyList<string> GetRootNames() => dbService.GetRootNames();
}
