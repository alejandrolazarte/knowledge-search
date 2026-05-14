using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_PollingChangeSourceDetectsModifiedFile : IDisposable
{
    readonly string _watchedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    readonly string _markdownFilePath;

    public When_PollingChangeSourceDetectsModifiedFile()
    {
        Directory.CreateDirectory(_watchedDirectory);
        _markdownFilePath = Path.Combine(_watchedDirectory, "existing-doc.md");
        File.WriteAllText(_markdownFilePath, "# original content");
    }

    [Fact]
    public async Task Then_Yields_Updated_Event()
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
        File.WriteAllText(_markdownFilePath, "# modified content");

        try { await watchTask; } catch (OperationCanceledException) { }

        var updatedEvent = detectedEvents.ShouldHaveSingleItem();
        updatedEvent.EventType.ShouldBe("updated");
        updatedEvent.Path.ShouldEndWith("existing-doc.md");
    }

    public void Dispose()
    {
        Directory.Delete(_watchedDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
