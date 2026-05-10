using KnowledgeSearch;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphRepositorySearches : IDisposable
{
    private readonly string _dbPath = Path.GetTempFileName();
    private readonly CodeGraphRepository _sut;

    public When_CodeGraphRepositorySearches()
    {
        _sut = new CodeGraphRepository(_dbPath);
    }

    [Fact]
    public void Then_SearchNodesByNameReturnsMatchingNodes()
    {
        var nodes = new List<CodeNode>
        {
            new("App.UserService", "UserService", CodeNodeKind.Class, "/src/UserService.cs", 1),
            new("App.OrderRepository", "OrderRepository", CodeNodeKind.Class, "/src/OrderRepository.cs", 1),
        };
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(nodes, [], 2, 0));

        var result = _sut.SearchNodes("my-repo", "Service");

        result.ShouldHaveSingleItem();
        result[0].Name.ShouldBe("UserService");
    }

    [Fact]
    public void Then_SearchNodesIsCaseInsensitive()
    {
        var nodes = new List<CodeNode>
        {
            new("App.UserService", "UserService", CodeNodeKind.Class, "/src/UserService.cs", 1),
        };
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(nodes, [], 1, 0));

        var result = _sut.SearchNodes("my-repo", "userservice");

        result.ShouldHaveSingleItem();
    }

    [Fact]
    public void Then_SearchNodesReturnsEmptyListWhenNothingMatches()
    {
        var nodes = new List<CodeNode>
        {
            new("App.UserService", "UserService", CodeNodeKind.Class, "/src/UserService.cs", 1),
        };
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(nodes, [], 1, 0));

        var result = _sut.SearchNodes("my-repo", "NonExistent");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Then_SearchNodesOnlyReturnsResultsFromRequestedRepository()
    {
        var alphaNodes = new List<CodeNode>
        {
            new("Alpha.UserService", "UserService", CodeNodeKind.Class, "/alpha/UserService.cs", 1),
        };
        var betaNodes = new List<CodeNode>
        {
            new("Beta.UserService", "UserService", CodeNodeKind.Class, "/beta/UserService.cs", 1),
        };
        _sut.SaveScanResult("repo-alpha", new CodeGraphScanResult(alphaNodes, [], 1, 0));
        _sut.SaveScanResult("repo-beta", new CodeGraphScanResult(betaNodes, [], 1, 0));

        var result = _sut.SearchNodes("repo-alpha", "UserService");

        result.ShouldHaveSingleItem();
        result[0].Identifier.ShouldBe("Alpha.UserService");
    }

    public void Dispose()
    {
        _sut.Dispose();
        File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }
}
