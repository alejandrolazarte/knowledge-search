using System.Threading.Channels;

namespace KnowledgeSearch;

interface ILogService
{
    void Append(LogEvent ev);
    List<LogEvent> ReadLast(int n);
    Channel<LogEvent> Subscribe();
    void Unsubscribe(ChannelWriter<LogEvent> writer);
}
