using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_PollingChangeSourceEncountersNonMarkdown : IDisposable
{
    readonly string _watchedDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public When_PollingChangeSourceEncountersNonMarkdown()
    {
        Directory.CreateDirectory(_watchedDirectory);
    }

    [Fact]
    public async Task Then_Yields_No_Event()
    {
        var sut = new PollingChangeSource(TimeSpan.FromMilliseconds(50));
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var detectedEvents = new List<FileChangeEvent>();
        var watchTask = Task.Run(async () =>
        {
            await foreach (var fileChangeEvent in sut.WatchAsync([_watchedDirectory], cancellationTokenSource.Token))
            {
                detectedEvents.Add(fileChangeEvent);
            }
        });

        await Task.Delay(100);
        File.WriteAllText(Path.Combine(_watchedDirectory, "config.json"), "{}");
        File.WriteAllText(Path.Combine(_watchedDirectory, "image.png"), "fake-png");

        try { await watchTask; } catch (OperationCanceledException) { }

        detectedEvents.ShouldBeEmpty();
    }

    public void Dispose()
    {
        Directory.Delete(_watchedDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
