namespace KnowledgeSearch.Core.Domain.CodeGraph;

public sealed record CodeDocument(
    string RepositoryName,
    string Identifier,
    string Name,
    CodeNodeKind Kind,
    string FilePath,
    int Line,
    string Content);

public sealed record CodeDocumentSearchResult(
    string RepositoryName,
    string Identifier,
    string Name,
    CodeNodeKind Kind,
    string FilePath,
    int Line,
    string Content,
    double Score);
