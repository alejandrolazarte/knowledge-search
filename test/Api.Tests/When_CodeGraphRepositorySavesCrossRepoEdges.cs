using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphRepositorySavesCrossRepoEdges : IDisposable
{
    private readonly string _dbPath = Path.GetTempFileName();
    private readonly CodeGraphRepository _sut;

    public When_CodeGraphRepositorySavesCrossRepoEdges()
    {
        _sut = new CodeGraphRepository(_dbPath);
    }

    [Fact]
    public void Then_SavesAndRetrievesCrossRepoEdges()
    {
        var edges = new List<CrossRepoCodeEdge>
        {
            new("notifications-ms", "Notif.Handler", "users-ms", "Users.Event", CrossRepoEdgeKind.References),
        };

        _sut.SaveCrossRepoEdges(edges);
        var retrieved = _sut.GetCrossRepoEdges();

        retrieved.ShouldHaveSingleItem();
        retrieved[0].SourceRepositoryName.ShouldBe("notifications-ms");
        retrieved[0].SourceIdentifier.ShouldBe("Notif.Handler");
        retrieved[0].TargetRepositoryName.ShouldBe("users-ms");
        retrieved[0].TargetIdentifier.ShouldBe("Users.Event");
        retrieved[0].Kind.ShouldBe(CrossRepoEdgeKind.References);
    }

    [Fact]
    public void Then_ReplacesAllCrossRepoEdgesOnSave()
    {
        var firstBatch = new List<CrossRepoCodeEdge>
        {
            new("repo-a", "A.OldClass", "repo-b", "B.Something", CrossRepoEdgeKind.References),
        };
        _sut.SaveCrossRepoEdges(firstBatch);

        var secondBatch = new List<CrossRepoCodeEdge>
        {
            new("repo-a", "A.NewClass", "repo-b", "B.Something", CrossRepoEdgeKind.References),
        };
        _sut.SaveCrossRepoEdges(secondBatch);

        var retrieved = _sut.GetCrossRepoEdges();

        retrieved.ShouldHaveSingleItem();
        retrieved[0].SourceIdentifier.ShouldBe("A.NewClass");
    }

    [Fact]
    public void Then_ReturnsEmptyListWhenNoCrossRepoEdgesExist()
    {
        var result = _sut.GetCrossRepoEdges();

        result.ShouldBeEmpty();
    }

    public void Dispose()
    {
        _sut.Dispose();
        File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }
}
