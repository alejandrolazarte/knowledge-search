using System.Text.Json.Serialization;

namespace KnowledgeSearch.Core.Abstractions.Jobs;

#pragma warning disable CA1711 // "Queue" en el nombre describe la abstraccion mejor que cualquier alternativa
public interface IJobQueue
#pragma warning restore CA1711
{
    Task<Guid> EnqueueAsync(IJob job, CancellationToken cancellationToken = default);
    JobStatus? TryGetStatus(Guid jobId);
}

public sealed record JobStatus(
    Guid JobId,
    string Kind,
    JobState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

[JsonConverter(typeof(JsonStringEnumConverter<JobState>))]
public enum JobState
{
    Queued,
    Running,
    Completed,
    Failed,
}
