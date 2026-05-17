using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.CodeGraph;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch.Core.Abstractions.CodeGraph;

public interface ICodeGraphStore
{
    bool RepositoryExists(string repositoryName);
    IReadOnlyList<CodeNode> GetNodes(string repositoryName);
    IReadOnlyList<CodeEdge> GetEdges(string repositoryName);
    IReadOnlyList<string> GetRepositoryNames();
    Result<IReadOnlyList<CodeDocumentSearchResult>> SearchCodeDocuments(
        string query,
        int limit,
        SearchMode modes,
        IReadOnlyList<string>? repositories,
        IReadOnlyList<CodeNodeKind>? kinds);
    bool IsCodePathAllowed(string fullPath);
    IReadOnlyList<CrossRepoCodeEdge> GetCrossRepoEdges();
}
