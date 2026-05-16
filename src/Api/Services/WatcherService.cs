using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch;

internal sealed class WatcherService(
    ISourceConfigurationService sourceConfiguration,
    IDbService dbService,
    ILogService log,
    IFileChangeSource changeSource) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var currentRoots = GetAccessibleRoots();

        while (!cancellationToken.IsCancellationRequested)
        {
            using var watchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var watchTask = RunWatchLoop(currentRoots, watchCts.Token);

            while (!cancellationToken.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }

                var newRoots = GetAccessibleRoots();
                if (!RootsEqual(currentRoots, newRoots))
                {
                    currentRoots = newRoots;
                    await watchCts.CancelAsync().ConfigureAwait(false);
                    break;
                }
            }

            try { await watchTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task RunWatchLoop(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var fileChangeEvent in changeSource.WatchAsync(roots, cancellationToken).ConfigureAwait(false))
            {
                if (fileChangeEvent.EventType == "deleted")
                {
                    ProcessDelete(fileChangeEvent.Path);
                }
                else
                {
                    ProcessChange(fileChangeEvent.Path, fileChangeEvent.EventType);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "error",
                $"Watcher stopped unexpectedly: {ex.Message}"));
        }
    }

    internal void ProcessChange(string path, string eventType)
    {
        try
        {
            dbService.ReindexFile(path);
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), eventType, GetRelativePath(path)));
        }
        catch (Exception ex)
        {
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "error", ex.Message));
        }
    }

    internal void ProcessDelete(string path)
    {
        try
        {
            dbService.DeleteFile(path);
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "deleted", GetRelativePath(path)));
        }
        catch (Exception ex)
        {
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "error", ex.Message));
        }
    }

    private string[] GetAccessibleRoots() =>
        sourceConfiguration.GetConfiguration()
            .Sources
            .Select(s => s.ToConfiguredSource())
            .Where(s => s.IndexDocs)
            .Select(s => s.GetAccessiblePath())
            .ToArray();

    private string GetRelativePath(string path)
    {
        var roots = GetAccessibleRoots();
        foreach (var root in roots)
        {
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(root, path).Replace('\\', '/');
            }
        }
        return path.Replace('\\', '/');
    }

    private static bool RootsEqual(string[] a, string[] b) =>
        a.Length == b.Length &&
        a.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(
                b.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
}
