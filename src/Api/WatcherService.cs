namespace KnowledgeSearch;

class WatcherService(string docsDir, string dbPath, ILogService log) : BackgroundService
{
    readonly Dictionary<string, Timer> _debounce = [];
    readonly object _dlock = new();

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        if (!Directory.Exists(docsDir)) return Task.CompletedTask;

        var watcher = new FileSystemWatcher(docsDir)
        {
            Filter               = "*",
            IncludeSubdirectories = true,
            EnableRaisingEvents  = true,
            NotifyFilter         = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };

        watcher.Changed += (_, e) => { if (IsMd(e.FullPath)) Debounce(e.FullPath, "updated"); };
        watcher.Created += (_, e) => { if (IsMd(e.FullPath)) Debounce(e.FullPath, "added");   };
        watcher.Deleted += (_, e) => { if (IsMd(e.FullPath)) ProcessDelete(e.FullPath);        };
        watcher.Renamed += (_, e) =>
        {
            if (IsMd(e.OldFullPath)) ProcessDelete(e.OldFullPath);
            if (IsMd(e.FullPath))    Debounce(e.FullPath, "added");
        };

        ct.Register(() => watcher.Dispose());
        return Task.Delay(Timeout.Infinite, ct);
    }

    static bool IsMd(string path) =>
        path.EndsWith(".md",  StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mkd", StringComparison.OrdinalIgnoreCase);

    void Debounce(string path, string type)
    {
        lock (_dlock)
        {
            if (_debounce.TryGetValue(path, out var t)) t.Dispose();
            _debounce[path] = new Timer(_ => ProcessChange(path, type), null, 500, Timeout.Infinite);
        }
    }

    internal void ProcessChange(string path, string type)
    {
        try
        {
            if (!File.Exists(path)) return;

            using var con = DbService.Open(dbPath);
            DbService.EnsureSchema(con);

            long  mtime  = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
            long? stored = DbService.QueryLong(con, "SELECT last_modified FROM docs_meta WHERE path=@p", path);
            if (stored == mtime) return;

            if (stored is not null)
                DbService.ExecP(con, "DELETE FROM docs WHERE path=@p", path);

            DbService.IndexFile(con, path, Path.GetFileNameWithoutExtension(path));
            DbService.ExecP2(con,
                "INSERT INTO docs_meta(path,last_modified) VALUES(@p,@m) ON CONFLICT(path) DO UPDATE SET last_modified=excluded.last_modified",
                path, mtime);

            var rel = Path.GetRelativePath(docsDir, path).Replace('\\', '/');
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), type, rel));
        }
        catch { }
    }

    internal void ProcessDelete(string path)
    {
        try
        {
            using var con = DbService.Open(dbPath);
            DbService.ExecP(con, "DELETE FROM docs      WHERE path=@p", path);
            DbService.ExecP(con, "DELETE FROM docs_meta WHERE path=@p", path);

            var rel = Path.GetRelativePath(docsDir, path).Replace('\\', '/');
            log.Append(new LogEvent(DateTime.UtcNow.ToString("o"), "deleted", rel));
        }
        catch { }
    }
}
