using KnowledgeSearch.Core.Abstractions.Jobs;
using Microsoft.Extensions.Hosting;

namespace KnowledgeSearch;

public interface IJobContextFactory
{
    IJobContext Create(Guid jobId);
}

/// <summary>
/// Consumidor unico de la cola: procesa un job a la vez hasta terminar antes
/// de pedir el siguiente. La serializacion elimina contienda en SQLite.
/// </summary>
public sealed class JobWorker(ChannelJobQueue queue, IJobContextFactory contextFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var queued in queue.ReadAllAsync(stoppingToken))
            {
                await RunJobAsync(queued, stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunJobAsync(QueuedJob queued, CancellationToken cancellationToken)
    {
        queue.TransitionToRunning(queued.Id);
        try
        {
            var context = contextFactory.Create(queued.Id);
            await queued.Job.ExecuteAsync(context, cancellationToken);
            queue.TransitionToCompleted(queued.Id);
        }
        catch (Exception ex)
        {
            queue.TransitionToFailed(queued.Id, ex.Message);
        }
    }
}
