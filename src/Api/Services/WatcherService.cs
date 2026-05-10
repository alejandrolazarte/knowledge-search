namespace KnowledgeSearch;

internal sealed class WatcherService(
    IReadOnlyList<string> roots,
    IDbService dbService,
    ILogService log) : BackgroundService
{
    private readonly Dictionary<string, Timer> _debounce = [];
    private readonly object _debounceLock = new();

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                var watcher = CreateWatcher(root);
                cancellationToken.Register(() => watcher.Dispose());
            }
            catch (Exception ex)
            {
                log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "error",
                    $"Failed to start watcher for {root}: {ex.Message}"));
            }
        }

        return Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private FileSystemWatcher CreateWatcher(string root)
    {
        var watcher = new FileSystemWatcher(root)
        {
            Filter = "*",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };

        watcher.Changed += (_, e) => { if (IsMd(e.FullPath)) { Debounce(e.FullPath, "updated"); } };
        watcher.Created += (_, e) => { if (IsMd(e.FullPath)) { Debounce(e.FullPath, "added"); } };
        watcher.Deleted += (_, e) => { if (IsMd(e.FullPath)) { ProcessDelete(e.FullPath); } };
        watcher.Renamed += (_, e) =>
        {
            if (IsMd(e.OldFullPath))
            {
                ProcessDelete(e.OldFullPath);
            }

            if (IsMd(e.FullPath))
            {
                // Real rename between .md files → "added" at destination.
                // Atomic write (temp → target rename) where source was not .md → "updated".
                var eventType = IsMd(e.OldFullPath) ? "added" : "updated";
                Debounce(e.FullPath, eventType);
            }
        };

        return watcher;
    }

    private static bool IsMd(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mkd", StringComparison.OrdinalIgnoreCase);

    private void Debounce(string path, string eventType)
    {
        lock (_debounceLock)
        {
            if (_debounce.TryGetValue(path, out var existing))
            {
                existing.Dispose();
            }

            Timer? timer = null;
            timer = new Timer(_ =>
            {
                try
                {
                    ProcessChange(path, eventType);
                }
                finally
                {
                    lock (_debounceLock)
                    {
                        if (_debounce.TryGetValue(path, out var current) && ReferenceEquals(current, timer))
                        {
                            _debounce.Remove(path);
                            current.Dispose();
                        }
                    }
                }
            }, null, 500, Timeout.Infinite);

            _debounce[path] = timer;
        }
    }

    internal void ProcessChange(string path, string eventType)
    {
        try
        {
            dbService.ReindexFile(path);
            var relativePath = GetRelativePath(path);
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), eventType, relativePath));
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
            var relativePath = GetRelativePath(path);
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "deleted", relativePath));
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
