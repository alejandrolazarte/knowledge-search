using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_PollingChangeSourceDetectsDeletedFile : IDisposable
{
    readonly string _watchedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    readonly string _markdownFilePath;

    public When_PollingChangeSourceDetectsDeletedFile()
    {
        Directory.CreateDirectory(_watchedDirectory);
        _markdownFilePath = Path.Combine(_watchedDirectory, "to-delete.md");
        File.WriteAllText(_markdownFilePath, "# will be deleted");
    }

    [Fact]
    public async Task Then_Yields_Deleted_Event()
    {
        var sut = new PollingChangeSource(TimeSpan.FromMilliseconds(50));
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var detectedEvents = new List<FileChangeEvent>();
        var watchTask = Task.Run(async () =>
        {
            await foreach (var fileChangeEvent in sut.WatchAsync([_watchedDirectory], cancellationTokenSource.Token))
            {
                detectedEvents.Add(fileChangeEvent);
                cancellationTokenSource.Cancel();
            }
        });

        await Task.Delay(150);
        File.Delete(_markdownFilePath);

        try { await watchTask; } catch (OperationCanceledException) { }

        var deletedEvent = detectedEvents.ShouldHaveSingleItem();
        deletedEvent.EventType.ShouldBe("deleted");
        deletedEvent.Path.ShouldEndWith("to-delete.md");
    }

    public void Dispose()
    {
        if (Directory.Exists(_watchedDirectory))
        {
            Directory.Delete(_watchedDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
