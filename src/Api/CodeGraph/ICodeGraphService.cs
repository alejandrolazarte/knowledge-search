namespace KnowledgeSearch;

internal interface ICodeGraphService
{
    CodeGraphScanResult ScanDirectory(string directoryPath);
    CodeSubgraphResult SearchSubgraph(string repositoryName, string query, int depth);
    CrossRepoSubgraphResult SearchSubgraphAcrossRepositories(string query, int depth);
    IReadOnlyList<CrossRepoCodeEdge> BuildCrossRepoEdges();
}
