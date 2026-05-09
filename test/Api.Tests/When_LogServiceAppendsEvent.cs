using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_LogServiceAppendsEvent : IDisposable
{
    readonly string _logPath = Path.GetTempFileName();
    readonly LogService _sut;

    public When_LogServiceAppendsEvent() => _sut = new LogService(_logPath);

    [Fact]
    public void Then_EventIsPersistedToFileAndReadBack()
    {
        var ev = new LogEvent(DateTime.UtcNow.ToString("o"), "added", "docs/test.md");

        _sut.Append(ev);

        var result = _sut.ReadLast(10);
        result.ShouldHaveSingleItem();
        result[0].Type.ShouldBe("added");
        result[0].Path.ShouldBe("docs/test.md");
    }

    public void Dispose()
    {
        File.Delete(_logPath);
        GC.SuppressFinalize(this);
    }
}
