using KnowledgeSearch.Core.Abstractions.Jobs;

namespace KnowledgeSearch;

public sealed class LogServiceJobContextFactory(ILogService log) : IJobContextFactory
{
    public IJobContext Create(Guid jobId) => new LogServiceJobContext(jobId, log);
}

internal sealed class LogServiceJobContext(Guid jobId, ILogService log) : IJobContext
{
    public Guid JobId { get; } = jobId;

    public Task ReportProgressAsync(string message, CancellationToken cancellationToken = default)
    {
        log.Append(new LogEvent(
            DateTime.UtcNow.ToString("o"),
            $"job:{JobId}",
            message));
        return Task.CompletedTask;
    }
}
