namespace KnowledgeSearch.Core.Domain.Search;

public sealed record SearchResult(
    string Title,
    string Section,
    string Path,
    int Line,
    string Content,
    string Root);
