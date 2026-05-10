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

    [Fact]
    public void Then_SearchCodeDocumentsReturnsMatchesFromIndexedContent()
    {
        var filePath = WriteTempSourceFile("""
            namespace MyApp;
            public class UserService
            {
                public void Handle()
                {
                    var token = "ImportantToken";
                }
            }
            """);
        var nodes = new List<CodeNode>
        {
            new("MyApp.UserService", "UserService", CodeNodeKind.Class, filePath, 2),
        };
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(nodes, [], 1, 0));

        var result = _sut.SearchCodeDocuments("ImportantToken", 10, SearchMode.Default, null, null);

        result.ShouldHaveSingleItem();
        result[0].RepositoryName.ShouldBe("my-repo");
        result[0].Name.ShouldBe("UserService");
        result[0].Content.ShouldContain("ImportantToken");
    }

    [Fact]
    public void Then_SearchCodeDocumentsFiltersByRepositoryAndKind()
    {
        var alphaFile = WriteTempSourceFile("public class AlphaService { string value = \"Needle\"; }");
        var betaFile = WriteTempSourceFile("public interface BetaService { string Needle(); }");
        _sut.SaveScanResult("repo-alpha", new CodeGraphScanResult(
            [new("AlphaService", "AlphaService", CodeNodeKind.Class, alphaFile, 1)],
            [],
            1,
            0));
        _sut.SaveScanResult("repo-beta", new CodeGraphScanResult(
            [new("BetaService", "BetaService", CodeNodeKind.Interface, betaFile, 1)],
            [],
            1,
            0));

        var result = _sut.SearchCodeDocuments(
            "Needle",
            10,
            SearchMode.Default,
            ["repo-beta"],
            [CodeNodeKind.Interface]);

        result.ShouldHaveSingleItem();
        result[0].RepositoryName.ShouldBe("repo-beta");
        result[0].Kind.ShouldBe(CodeNodeKind.Interface);
    }

    [Fact]
    public void Then_RescanningRepositoryReplacesCodeDocuments()
    {
        var filePath = WriteTempSourceFile("public class UserService { string value = \"OldNeedle\"; }");
        var nodes = new List<CodeNode>
        {
            new("UserService", "UserService", CodeNodeKind.Class, filePath, 1),
        };
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(nodes, [], 1, 0));

        File.WriteAllText(filePath, "public class UserService { string value = \"NewNeedle\"; }");
        _sut.SaveScanResult("my-repo", new CodeGraphScanResult(nodes, [], 1, 0));

        _sut.SearchCodeDocuments("OldNeedle", 10, SearchMode.Default, null, null).ShouldBeEmpty();
        _sut.SearchCodeDocuments("NewNeedle", 10, SearchMode.Default, null, null).ShouldHaveSingleItem();
    }

    public void Dispose()
    {
        _sut.Dispose();
        File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }

    private static string WriteTempSourceFile(string source)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cs");
        File.WriteAllText(path, source);
        return path;
    }
}
