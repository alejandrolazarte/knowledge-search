namespace KnowledgeSearch;

internal record CodeDocument(
    string RepositoryName,
    string Identifier,
    string Name,
    CodeNodeKind Kind,
    string FilePath,
    int Line,
    string Content);

internal record CodeDocumentSearchResult(
    string RepositoryName,
    string Identifier,
    string Name,
    CodeNodeKind Kind,
    string FilePath,
    int Line,
    string Content,
    double Score);
