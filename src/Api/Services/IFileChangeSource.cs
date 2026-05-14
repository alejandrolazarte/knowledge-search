namespace KnowledgeSearch;

internal interface IFileChangeSource
{
    IAsyncEnumerable<FileChangeEvent> WatchAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken);
}
