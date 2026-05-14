using System.Threading.Channels;

namespace KnowledgeSearch;

internal sealed class FileSystemChangeSource : IFileChangeSource
{
    public async IAsyncEnumerable<FileChangeEvent> WatchAsync(
        IReadOnlyList<string> roots,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<FileChangeEvent>();

        var watchers = roots
            .Where(Directory.Exists)
            .Select(root => CreateWatcher(root, channel.Writer))
            .ToList();

        try
        {
            await foreach (var fileChangeEvent in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return fileChangeEvent;
            }
        }
        finally
        {
            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }

            channel.Writer.TryComplete();
        }
    }

    private static FileSystemWatcher CreateWatcher(string root, ChannelWriter<FileChangeEvent> writer)
    {
        var debounce = new Dictionary<string, Timer>();
        var debounceLock = new object();

        void Debounce(string path, string eventType)
        {
            lock (debounceLock)
            {
                if (debounce.TryGetValue(path, out var existing))
                {
                    existing.Dispose();
                }

                Timer? timer = null;
                timer = new Timer(_ =>
                {
                    writer.TryWrite(new FileChangeEvent(path, eventType));
                    lock (debounceLock)
                    {
                        if (debounce.TryGetValue(path, out var current) && ReferenceEquals(current, timer))
                        {
                            debounce.Remove(path);
                            current.Dispose();
                        }
                    }
                }, null, 500, Timeout.Infinite);

                debounce[path] = timer;
            }
        }

        var watcher = new FileSystemWatcher(root)
        {
            Filter = "*",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };

        watcher.Changed += (_, e) => { if (IsMarkdown(e.FullPath)) { Debounce(e.FullPath, "updated"); } };
        watcher.Created += (_, e) => { if (IsMarkdown(e.FullPath)) { Debounce(e.FullPath, "added"); } };
        watcher.Deleted += (_, e) => { if (IsMarkdown(e.FullPath)) { writer.TryWrite(new FileChangeEvent(e.FullPath, "deleted")); } };
        watcher.Renamed += (_, e) =>
        {
            if (IsMarkdown(e.OldFullPath))
            {
                writer.TryWrite(new FileChangeEvent(e.OldFullPath, "deleted"));
            }

            if (IsMarkdown(e.FullPath))
            {
                var eventType = IsMarkdown(e.OldFullPath) ? "added" : "updated";
                Debounce(e.FullPath, eventType);
            }
        };

        return watcher;
    }

    private static bool IsMarkdown(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mkd", StringComparison.OrdinalIgnoreCase);
}
