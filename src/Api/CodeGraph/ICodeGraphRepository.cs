namespace KnowledgeSearch;

internal interface ICodeGraphRepository : IDisposable
{
    void SaveScanResult(string repositoryName, CodeGraphScanResult scanResult);
    IReadOnlyList<CodeNode> GetNodes(string repositoryName);
    IReadOnlyList<CodeEdge> GetEdges(string repositoryName);
    IReadOnlyList<string> GetRepositoryNames();
}
