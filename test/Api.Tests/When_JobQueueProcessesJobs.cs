using KnowledgeSearch;
using KnowledgeSearch.Core.Abstractions.Jobs;
using Shouldly;
using Xunit;

namespace Api.Tests;

// Contrato esperado de IJobQueue:
//   - EnqueueAsync devuelve un Guid y deja el job en estado Queued.
//   - El worker (BackgroundService) lo procesa y pasa a Running -> Completed.
//   - Si el job lanza, el estado es Failed con el mensaje en Error.
//   - Dos jobs encolados se ejecutan SECUENCIALMENTE (no en paralelo) —
//     critico para evitar contienda en SQLite.
public class When_JobQueueProcessesJobs : IAsyncLifetime, IDisposable
{
    public void Dispose() => GC.SuppressFinalize(this);
    private ChannelJobQueue _queue = null!;
    private JobWorker _worker = null!;
    private CancellationTokenSource _cts = null!;

    public Task InitializeAsync()
    {
        _queue = new ChannelJobQueue();
        _worker = new JobWorker(_queue, new NullJobContextFactory());
        _cts = new CancellationTokenSource();
        _ = _worker.StartAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _cts.CancelAsync();
        await _worker.StopAsync(CancellationToken.None);
        _cts.Dispose();
    }

    [Fact]
    public async Task Then_EnqueuedJobReachesCompletedState()
    {
        var job = new FakeJob("noop", _ => Task.CompletedTask);

        var id = await _queue.EnqueueAsync(job);

        await WaitForState(id, JobState.Completed);
        _queue.TryGetStatus(id)!.State.ShouldBe(JobState.Completed);
        _queue.TryGetStatus(id)!.Error.ShouldBeNull();
    }

    [Fact]
    public async Task Then_FailingJobReachesFailedStateWithError()
    {
        var job = new FakeJob("boom", _ => throw new InvalidOperationException("kaboom"));

        var id = await _queue.EnqueueAsync(job);

        await WaitForState(id, JobState.Failed);
        var status = _queue.TryGetStatus(id)!;
        status.State.ShouldBe(JobState.Failed);
        status.Error!.ShouldContain("kaboom");
    }

    [Fact]
    public async Task Then_JobsRunSequentiallyNotInParallel()
    {
        var concurrent = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        Task RunJob(IJobContext _)
        {
            lock (lockObj)
            {
                concurrent++;
                maxConcurrent = Math.Max(maxConcurrent, concurrent);
            }
            return Task.Delay(50).ContinueWith(_ =>
            {
                lock (lockObj) { concurrent--; }
            });
        }

        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(await _queue.EnqueueAsync(new FakeJob("seq", RunJob)));
        }

        foreach (var id in ids)
        {
            await WaitForState(id, JobState.Completed);
        }

        maxConcurrent.ShouldBe(1);
    }

    private async Task WaitForState(Guid id, JobState target)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            var status = _queue.TryGetStatus(id);
            if (status is not null && status.State == target)
            {
                return;
            }
            await Task.Delay(20);
        }
        throw new TimeoutException($"Job {id} no alcanzo {target}");
    }

    private sealed class FakeJob(string kind, Func<IJobContext, Task> body) : IJob
    {
        public string Kind => kind;
        public Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken) => body(context);
    }

    private sealed class NullJobContextFactory : IJobContextFactory
    {
        public IJobContext Create(Guid jobId) => new NullJobContext(jobId);
    }

    private sealed class NullJobContext(Guid jobId) : IJobContext
    {
        public Guid JobId { get; } = jobId;
        public Task ReportProgressAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
