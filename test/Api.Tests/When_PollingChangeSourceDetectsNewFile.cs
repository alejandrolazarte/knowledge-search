using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_PollingChangeSourceDetectsNewFile : IDisposable
{
    readonly string _watchedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public When_PollingChangeSourceDetectsNewFile()
    {
        Directory.CreateDirectory(_watchedDirectory);
    }

    [Fact]
    public async Task Then_Yields_Added_Event()
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

        await Task.Delay(150); // let first snapshot settle
        File.WriteAllText(Path.Combine(_watchedDirectory, "new-doc.md"), "# content");

        try { await watchTask; } catch (OperationCanceledException) { }

        var addedEvent = detectedEvents.ShouldHaveSingleItem();
        addedEvent.EventType.ShouldBe("added");
        addedEvent.Path.ShouldEndWith("new-doc.md");
    }

    public void Dispose()
    {
        Directory.Delete(_watchedDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
