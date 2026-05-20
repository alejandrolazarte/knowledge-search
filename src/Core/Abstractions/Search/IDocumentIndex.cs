using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch.Core.Abstractions.Search;

public interface IDocumentIndex
{
    void UpdateRoots(IReadOnlyList<string> roots);
    void UpdateSources(IReadOnlyList<ConfiguredSource> sources);
}
