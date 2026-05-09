using System.Text.Json;
using System.Threading.Channels;

namespace KnowledgeSearch;

class LogService(string logPath) : ILogService
{
    readonly object _lock = new();
    readonly List<ChannelWriter<LogEvent>> _subs = [];

    // Case-insensitive options for reading old log entries that may have
    // been written with a different casing (e.g. PascalCase before the fix).
    static readonly JsonSerializerOptions _readOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolverChain        = { AppJsonContext.Default },
    };

    public void Append(LogEvent ev)
    {
        var line = JsonSerializer.Serialize(ev, AppJsonContext.Default.LogEvent);
        File.AppendAllText(logPath, line + "\n");
        lock (_lock)
            foreach (var sub in _subs) sub.TryWrite(ev);
    }

    public List<LogEvent> ReadLast(int n)
    {
        if (!File.Exists(logPath)) return [];
        var lines = File.ReadAllLines(logPath);
        var result = new List<LogEvent>();
        foreach (var line in lines.TakeLast(n))
        {
            try
            {
                var ev = JsonSerializer.Deserialize<LogEvent>(line, _readOpts);
                if (ev is not null) result.Add(ev);
            }
            catch { }
        }
        return result;
    }

    public Channel<LogEvent> Subscribe()
    {
        var ch = Channel.CreateUnbounded<LogEvent>();
        lock (_lock) _subs.Add(ch.Writer);
        return ch;
    }

    public void Unsubscribe(ChannelWriter<LogEvent> writer)
    {
        lock (_lock) _subs.Remove(writer);
        writer.TryComplete();
    }
}
