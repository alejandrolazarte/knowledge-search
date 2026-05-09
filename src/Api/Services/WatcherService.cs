namespace KnowledgeSearch;

internal sealed class WatcherService(string docsDir, string dbPath, ILogService log) : BackgroundService
{
    private readonly Dictionary<string, Timer> _debounce = [];
    private readonly object _debounceLock = new();

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(docsDir))
        {
            return Task.CompletedTask;
        }

        var watcher = new FileSystemWatcher(docsDir)
        {
            Filter                = "*",
            IncludeSubdirectories = true,
            EnableRaisingEvents   = true,
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.FileName,
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

        cancellationToken.Register(() => watcher.Dispose());
        return Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private static bool IsMd(string path) =>
        path.EndsWith(".md",  StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mkd", StringComparison.OrdinalIgnoreCase);

    private void Debounce(string path, string eventType)
    {
        lock (_debounceLock)
        {
            if (_debounce.TryGetValue(path, out var existing))
            {
                existing.Dispose();
            }

            _debounce[path] = new Timer(_ => ProcessChange(path, eventType), null, 500, Timeout.Infinite);
        }
    }

    internal void ProcessChange(string path, string eventType)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            using var connection = DbService.Open(dbPath);
            DbService.EnsureSchema(connection);

            // Mtime check is intentionally skipped here — the watcher already guarantees
            // a change occurred. Checking mtime would cause false negatives for atomic writes
            // (write-to-temp + rename) which preserve the original file's LastWriteTime.
            var modifiedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
            var stored     = DbService.QueryFirstLong(connection, "SELECT last_modified FROM docs_meta WHERE path=@path", path);

            if (stored is not null)
            {
                DbService.Execute(connection, "DELETE FROM docs WHERE path=@path", path);
            }

            DbService.IndexFile(connection, path, Path.GetFileNameWithoutExtension(path));
            DbService.Execute(connection,
                "INSERT INTO docs_meta(path,last_modified) VALUES(@path,@modifiedAt) ON CONFLICT(path) DO UPDATE SET last_modified=excluded.last_modified",
                path, modifiedAt);

            var relativePath = Path.GetRelativePath(docsDir, path).Replace('\\', '/');
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), eventType, relativePath));
        }
        catch (Exception)
        {
            // Best-effort: watcher operations must not crash the host.
        }
    }

    internal void ProcessDelete(string path)
    {
        try
        {
            using var connection = DbService.Open(dbPath);
            DbService.Execute(connection, "DELETE FROM docs      WHERE path=@path", path);
            DbService.Execute(connection, "DELETE FROM docs_meta WHERE path=@path", path);

            var relativePath = Path.GetRelativePath(docsDir, path).Replace('\\', '/');
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "deleted", relativePath));
        }
        catch (Exception)
        {
            // Best-effort: watcher operations must not crash the host.
        }
    }
}
