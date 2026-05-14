using KnowledgeSearch;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_WatcherServiceDetectsChange : IDisposable
{
    readonly string _docsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    readonly Mock<IDbService> _mockDb = new();
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

        using (var sut = new WatcherService([_docsDir], _mockDb.Object, _mockLog.Object, new Mock<IFileChangeSource>().Object))
        {
            sut.ProcessChange(filePath, "added");

            _mockDb.Verify(db => db.ReindexFile(filePath), Times.Once);
            _mockLog.Verify(
                l => l.Append(It.Is<LogEvent>(e => e.Type == "added" && e.Path == "guide.md")),
                Times.Once);

            _mockLog.Invocations.ShouldHaveSingleItem();
        }
    }

    public void Dispose()
    {
        try
        {
            // Force garbage collection and finalization to close any file handles
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Try to delete with retries and longer delays
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    if (!Directory.Exists(_docsDir))
                    {
                        break;
                    }

                    try
                    {
                        foreach (var file in Directory.GetFiles(_docsDir, "*", System.IO.SearchOption.AllDirectories))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                    catch { }

                    try
                    {
                        Directory.Delete(_docsDir, recursive: true);
                    }
                    catch when (attempt < 9)
                    {
                        System.Threading.Thread.Sleep(100 + (attempt * 50));
                        continue;
                    }

                    break;
                }
                catch
                {
                    if (attempt >= 9)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(100);
                }
            }
        }
        catch
        {
            // Swallow any exceptions during cleanup
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }
}
