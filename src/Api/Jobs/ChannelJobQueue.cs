using System.Collections.Concurrent;
using System.Threading.Channels;
using KnowledgeSearch.Core.Abstractions.Jobs;

namespace KnowledgeSearch;

/// <summary>
/// Cola in-process de jobs basada en <see cref="Channel{T}"/> con consumidor unico —
/// garantiza un solo writer a SQLite a la vez. El estado vive en memoria; si se
/// reinicia el proceso se pierde. Para persistencia/reintentos, reemplazar por
/// otra implementacion de <see cref="IJobQueue"/>.
/// </summary>
#pragma warning disable CA1711
public sealed class ChannelJobQueue : IJobQueue
#pragma warning restore CA1711
{
    private readonly Channel<QueuedJob> _channel = Channel.CreateUnbounded<QueuedJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<Guid, JobStatus> _status = new();

    public Task<Guid> EnqueueAsync(IJob job, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var status = new JobStatus(
            JobId: id,
            Kind:  job.Kind,
            State: JobState.Queued,
            CreatedAt:   DateTimeOffset.UtcNow,
            StartedAt:   null,
            CompletedAt: null,
            Error:       null);
        _status[id] = status;
        if (!_channel.Writer.TryWrite(new QueuedJob(id, job)))
        {
            throw new InvalidOperationException("No se pudo encolar el job — canal cerrado.");
        }
        return Task.FromResult(id);
    }

    public JobStatus? TryGetStatus(Guid jobId) =>
        _status.TryGetValue(jobId, out var status) ? status : null;

    internal IAsyncEnumerable<QueuedJob> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    internal void TransitionToRunning(Guid jobId)
    {
        _status.AddOrUpdate(jobId,
            _ => throw new InvalidOperationException($"Job {jobId} desconocido."),
            (_, prev) => prev with { State = JobState.Running, StartedAt = DateTimeOffset.UtcNow });
    }

    internal void TransitionToCompleted(Guid jobId)
    {
        _status.AddOrUpdate(jobId,
            _ => throw new InvalidOperationException($"Job {jobId} desconocido."),
            (_, prev) => prev with { State = JobState.Completed, CompletedAt = DateTimeOffset.UtcNow });
    }

    internal void TransitionToFailed(Guid jobId, string error)
    {
        _status.AddOrUpdate(jobId,
            _ => throw new InvalidOperationException($"Job {jobId} desconocido."),
            (_, prev) => prev with { State = JobState.Failed, CompletedAt = DateTimeOffset.UtcNow, Error = error });
    }
}

internal sealed record QueuedJob(Guid Id, IJob Job);
