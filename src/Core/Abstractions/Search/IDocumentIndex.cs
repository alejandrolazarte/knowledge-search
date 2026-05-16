namespace KnowledgeSearch.Core.Abstractions.Search;

public interface IDocumentIndex
{
    void UpdateRoots(IReadOnlyList<string> roots);
}
