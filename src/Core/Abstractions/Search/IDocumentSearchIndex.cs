using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch.Core.Abstractions.Search;

public interface IDocumentSearchIndex
{
    Result<IReadOnlyList<SearchResult>> Search(
        string query,
        int limit,
        SearchMode modes,
        IReadOnlyList<string>? roots);

    IndexResult IndexDirectories();
    bool IsPathAllowed(string fullPath);
    IReadOnlyList<string> GetRootNames();
}
