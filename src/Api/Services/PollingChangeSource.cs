namespace KnowledgeSearch;

internal sealed class PollingChangeSource(TimeSpan pollingInterval) : IFileChangeSource
{
    public async IAsyncEnumerable<FileChangeEvent> WatchAsync(
        IReadOnlyList<string> roots,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var snapshot = TakeSnapshot(roots);

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(pollingInterval, cancellationToken).ConfigureAwait(false);

            var currentSnapshot = TakeSnapshot(roots);

            foreach (var (path, _) in currentSnapshot.Where(file => !snapshot.ContainsKey(file.Key)))
            {
                yield return new FileChangeEvent(path, "added");
            }

            foreach (var (path, lastWriteTime) in currentSnapshot.Where(file =>
                snapshot.TryGetValue(file.Key, out var previousWriteTime) && previousWriteTime != file.Value))
            {
                yield return new FileChangeEvent(path, "updated");
            }

            foreach (var deletedPath in snapshot.Keys.Except(currentSnapshot.Keys))
            {
                yield return new FileChangeEvent(deletedPath, "deleted");
            }

            snapshot = currentSnapshot;
        }
    }

    private static Dictionary<string, DateTime> TakeSnapshot(IReadOnlyList<string> roots) =>
        roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(IsMarkdown)
            .ToDictionary(filePath => filePath, File.GetLastWriteTimeUtc);

    private static bool IsMarkdown(string filePath) =>
        filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        filePath.EndsWith(".mkd", StringComparison.OrdinalIgnoreCase);
}
