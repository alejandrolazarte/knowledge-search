using KnowledgeSearch.Core.Domain.CodeGraph;

namespace KnowledgeSearch.Core.Abstractions.CodeGraph;

public interface ICodeGraphSearchService
{
    CodeGraphScanResult ScanDirectory(string directoryPath);
    CodeSubgraphResult SearchSubgraph(string repositoryName, string query, int depth);
    CrossRepoSubgraphResult SearchSubgraphAcrossRepositories(string query, int depth);
    IReadOnlyList<CrossRepoCodeEdge> BuildCrossRepoEdges();
}
