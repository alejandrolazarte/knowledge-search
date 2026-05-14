namespace KnowledgeSearch;

internal sealed class WatcherService(
    IReadOnlyList<string> roots,
    IDbService dbService,
    ILogService log,
    IFileChangeSource changeSource) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
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

    private string GetRelativePath(string path)
    {
        foreach (var root in roots)
        {
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(root, path).Replace('\\', '/');
            }
        }

        return path.Replace('\\', '/');
    }
}
