namespace KnowledgeSearch;

internal interface ICodeGraphRepository : IDisposable
{
    void SaveScanResult(string repositoryName, CodeGraphScanResult scanResult);
    bool RepositoryExists(string repositoryName);
    IReadOnlyList<CodeNode> GetNodes(string repositoryName);
    IReadOnlyList<CodeEdge> GetEdges(string repositoryName);
    IReadOnlyList<string> GetRepositoryNames();
    IReadOnlyList<CodeNode> SearchNodes(string repositoryName, string query);
}
