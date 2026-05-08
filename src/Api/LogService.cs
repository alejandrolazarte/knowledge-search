using System.Text.Json;
using System.Threading.Channels;

namespace KnowledgeSearch;

class LogService(string logPath)
{
    readonly object _lock = new();
    readonly List<ChannelWriter<LogEvent>> _subs = [];

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
                var ev = JsonSerializer.Deserialize(line, AppJsonContext.Default.LogEvent);
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
