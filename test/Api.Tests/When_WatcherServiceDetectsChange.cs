using KnowledgeSearch;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_WatcherServiceDetectsChange : IDisposable
{
    readonly string _docsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    readonly Mock<IDbService>  _mockDb  = new();
    readonly Mock<ILogService> _mockLog = new();

    public When_WatcherServiceDetectsChange()
    {
        Directory.CreateDirectory(_docsDir);
    }

    [Fact]
    public void Then_LogsAddedEventWithRelativePathWhenFileIsIndexed()
    {
        var filePath = Path.Combine(_docsDir, "guide.md");
        File.WriteAllText(filePath, "# Title\nSome content");

        var sut = new WatcherService([_docsDir], _mockDb.Object, _mockLog.Object);
        sut.ProcessChange(filePath, "added");

        _mockLog.Verify(
            l => l.Append(It.Is<LogEvent>(e => e.Type == "added" && e.Path == "guide.md")),
            Times.Once);

        _mockLog.Invocations.ShouldHaveSingleItem();
    }

    public void Dispose()
    {
        Directory.Delete(_docsDir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
