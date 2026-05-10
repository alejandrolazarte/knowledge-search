using System.Net;
using System.Net.Http.Json;
using KnowledgeSearch;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_RepoEndpointsAreRequested : IDisposable
{
    private readonly Mock<ICodeGraphRepository> _mockRepository = new();
    private readonly Mock<ICodeGraphService> _mockService = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public When_RepoEndpointsAreRequested()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                ReplaceService<IDbService>(services, new Mock<IDbService>().Object);
                ReplaceService<ILogService>(services, new Mock<ILogService>().Object);
                ReplaceService<ICodeGraphRepository>(services, _mockRepository.Object);
                ReplaceService<ICodeGraphService>(services, _mockService.Object);
            }));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Then_GetReposReturnsRepositoryNames()
    {
        _mockRepository.Setup(r => r.GetRepositoryNames()).Returns(["repo-alpha", "repo-beta"]);

        var response = await _client.GetAsync("/repos");
        var names = await response.Content.ReadFromJsonAsync<List<string>>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        names.ShouldNotBeNull();
        names.ShouldContain("repo-alpha");
        names.ShouldContain("repo-beta");
    }

    [Fact]
    public async Task Then_GetGraphReturnsNodesAndEdgesForExistingRepository()
    {
        var nodes = new List<CodeNode>
        {
            new("MyApp.MyService", "MyService", CodeNodeKind.Class, "/src/MyService.cs", 1),
        };
        var edges = new List<CodeEdge>
        {
            new("MyApp.MyService", "IMyService", CodeEdgeKind.Implements, 1),
        };

        _mockRepository.Setup(r => r.RepositoryExists("my-repo")).Returns(true);
        _mockRepository.Setup(r => r.GetNodes("my-repo")).Returns(nodes);
        _mockRepository.Setup(r => r.GetEdges("my-repo")).Returns(edges);

        var response = await _client.GetAsync("/repos/my-repo/graph");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CodeGraphApiResponse>();
        body.ShouldNotBeNull();
        body.Nodes.ShouldHaveSingleItem();
        body.Nodes[0].Name.ShouldBe("MyService");
        body.Nodes[0].Kind.ShouldBe("Class");
        body.Edges.ShouldHaveSingleItem();
        body.Edges[0].Kind.ShouldBe("Implements");
    }

    [Fact]
    public async Task Then_GetGraphReturnsNotFoundForUnknownRepository()
    {
        _mockRepository.Setup(r => r.RepositoryExists("nonexistent")).Returns(false);

        var response = await _client.GetAsync("/repos/nonexistent/graph");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Then_PostScanReturnsSummaryForExistingDirectory()
    {
        var existingDirectory = Path.GetTempPath();
        var scanResult = new CodeGraphScanResult(
            [new("MyApp.MyClass", "MyClass", CodeNodeKind.Class, "/src/MyClass.cs", 1)],
            [new("MyApp.MyClass", "IMyClass", CodeEdgeKind.Implements, 1)],
            FilesScanned: 5,
            FilesSkipped: 2);

        _mockService.Setup(s => s.ScanDirectory(existingDirectory)).Returns(scanResult);

        var response = await _client.PostAsJsonAsync("/repos/scan", new { directoryPath = existingDirectory });
        var summary = await response.Content.ReadFromJsonAsync<ScanSummaryApiResponse>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        summary.ShouldNotBeNull();
        summary.FilesScanned.ShouldBe(5);
        summary.FilesSkipped.ShouldBe(2);
        summary.NodesFound.ShouldBe(1);
        summary.EdgesFound.ShouldBe(1);
    }

    [Fact]
    public async Task Then_PostScanReturnsBadRequestWhenDirectoryPathIsEmpty()
    {
        var response = await _client.PostAsJsonAsync("/repos/scan", new { directoryPath = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Then_PostScanReturnsBadRequestWhenDirectoryDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync("/repos/scan", new { directoryPath = "/nonexistent/path/xyz" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void ReplaceService<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(implementation);
    }
}
