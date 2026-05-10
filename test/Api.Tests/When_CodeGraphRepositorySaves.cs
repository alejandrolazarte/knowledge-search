using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphRepositorySaves : IDisposable
{
    private readonly string _dbPath = Path.GetTempFileName();
    private readonly CodeGraphRepository _sut;

    public When_CodeGraphRepositorySaves()
    {
        _sut = new CodeGraphRepository(_dbPath);
    }

    [Fact]
    public void Then_PersistsNodesForRepository()
    {
        var nodes = new List<CodeNode>
        {
            new("MyApp.MyClass", "MyClass", CodeNodeKind.Class, "/src/MyClass.cs", 1),
            new("MyApp.MyClass.Execute", "Execute", CodeNodeKind.Method, "/src/MyClass.cs", 5),
        };
        var scanResult = new CodeGraphScanResult(nodes, [], 1, 0);

        _sut.SaveScanResult("my-repo", scanResult);
        var retrieved = _sut.GetNodes("my-repo");

        retrieved.Count.ShouldBe(2);
        retrieved.ShouldContain(n => n.Name == "MyClass" && n.Kind == CodeNodeKind.Class);
        retrieved.ShouldContain(n => n.Name == "Execute" && n.Kind == CodeNodeKind.Method);
    }

    [Fact]
    public void Then_PersistsEdgesForRepository()
    {
        var edges = new List<CodeEdge>
        {
            new("MyApp.MyClass", "MyApp.MyClass.Execute", CodeEdgeKind.Contains, 5),
            new("MyApp.MyClass", "IMyService", CodeEdgeKind.Implements, 1),
        };
        var scanResult = new CodeGraphScanResult([], edges, 1, 0);

        _sut.SaveScanResult("my-repo", scanResult);
        var retrieved = _sut.GetEdges("my-repo");

        retrieved.Count.ShouldBe(2);
        retrieved.ShouldContain(e => e.Kind == CodeEdgeKind.Contains && e.TargetIdentifier == "MyApp.MyClass.Execute");
        retrieved.ShouldContain(e => e.Kind == CodeEdgeKind.Implements && e.TargetIdentifier == "IMyService");
    }

    [Fact]
    public void Then_ReplacesAllDataWhenRepositoryIsRescanned()
    {
        var firstScanNodes = new List<CodeNode>
        {
            new("OldClass", "OldClass", CodeNodeKind.Class, "/src/Old.cs", 1),
        };
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(firstScanNodes, [], 1, 0));

        var secondScanNodes = new List<CodeNode>
        {
            new("NewClass", "NewClass", CodeNodeKind.Class, "/src/New.cs", 1),
        };
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(secondScanNodes, [], 1, 0));

        var retrieved = _sut.GetNodes("my-repo");

        retrieved.ShouldHaveSingleItem();
        retrieved[0].Name.ShouldBe("NewClass");
    }

    [Fact]
    public void Then_GetRepositoryNamesReturnsAllSavedRepositories()
    {
        _sut.SaveScanResult("repo-alpha", new CodeGraphScanResult([], [], 0, 0));
        _sut.SaveScanResult("repo-beta", new CodeGraphScanResult([], [], 0, 0));

        var names = _sut.GetRepositoryNames();

        names.ShouldContain("repo-alpha");
        names.ShouldContain("repo-beta");
    }

    [Fact]
    public void Then_GetNodesReturnsOnlyNodesForRequestedRepository()
    {
        var alphaNodes = new List<CodeNode>
        {
            new("Alpha.Service", "Service", CodeNodeKind.Class, "/alpha/Service.cs", 1),
        };
        var betaNodes = new List<CodeNode>
        {
            new("Beta.Handler", "Handler", CodeNodeKind.Class, "/beta/Handler.cs", 1),
        };

        _sut.SaveScanResult("repo-alpha", new CodeGraphScanResult(alphaNodes, [], 1, 0));
        _sut.SaveScanResult("repo-beta", new CodeGraphScanResult(betaNodes, [], 1, 0));

        var alphaResult = _sut.GetNodes("repo-alpha");

        alphaResult.ShouldHaveSingleItem();
        alphaResult[0].Name.ShouldBe("Service");
    }

    [Fact]
    public void Then_GetNodesReturnsEmptyListForUnknownRepository()
    {
        var result = _sut.GetNodes("nonexistent-repo");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Then_NodeLineNumberIsPreserved()
    {
        var nodes = new List<CodeNode>
        {
            new("MyApp.MyClass", "MyClass", CodeNodeKind.Class, "/src/MyClass.cs", 42),
        };
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(nodes, [], 1, 0));

        var retrieved = _sut.GetNodes("my-repo");

        retrieved[0].Line.ShouldBe(42);
        retrieved[0].FilePath.ShouldBe("/src/MyClass.cs");
        retrieved[0].Identifier.ShouldBe("MyApp.MyClass");
    }

    public void Dispose()
    {
        _sut.Dispose();
        File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }
}
