namespace KnowledgeSearch;

internal interface ICodeGraphRepository : IDisposable
{
    void SaveScanResult(string repositoryName, CodeGraphScanResult scanResult);
    bool RepositoryExists(string repositoryName);
    IReadOnlyList<CodeNode> GetNodes(string repositoryName);
    IReadOnlyList<CodeEdge> GetEdges(string repositoryName);
    IReadOnlyList<string> GetRepositoryNames();
    IReadOnlyList<CodeNode> SearchNodes(string repositoryName, string query);
    IReadOnlyList<CodeDocumentSearchResult> SearchCodeDocuments(
        string query,
        int limit,
        SearchMode modes,
        IReadOnlyList<string>? repositories,
        IReadOnlyList<CodeNodeKind>? kinds);
    bool IsCodePathAllowed(string fullPath);
    void SaveCrossRepoEdges(IReadOnlyList<CrossRepoCodeEdge> edges);
    IReadOnlyList<CrossRepoCodeEdge> GetCrossRepoEdges();
}
