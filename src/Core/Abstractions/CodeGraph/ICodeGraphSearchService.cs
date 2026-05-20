using KnowledgeSearch.Core.Domain.CodeGraph;
using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch.Core.Abstractions.CodeGraph;

public interface ICodeGraphSearchService
{
    CodeGraphScanResult ScanDirectory(string directoryPath);
    CodeGraphScanResult ScanDirectory(ConfiguredSource source);
    CodeSubgraphResult SearchSubgraph(string repositoryName, string query, int depth);
    CrossRepoSubgraphResult SearchSubgraphAcrossRepositories(string query, int depth);
    IReadOnlyList<CrossRepoCodeEdge> BuildCrossRepoEdges();
}
