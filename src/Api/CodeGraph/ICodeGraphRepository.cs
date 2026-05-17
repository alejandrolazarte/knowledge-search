using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch;

internal interface ICodeGraphRepository : ICodeGraphStore, IDisposable
{
    void SaveScanResult(string repositoryName, CodeGraphScanResult scanResult);
    IReadOnlyList<CodeNode> SearchNodes(string repositoryName, string query);
    void SaveCrossRepoEdges(IReadOnlyList<CrossRepoCodeEdge> edges);
}
