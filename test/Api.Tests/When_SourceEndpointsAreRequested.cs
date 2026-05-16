using System.Net;
using System.Net.Http.Json;
using KnowledgeSearch;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_SourceEndpointsAreRequested : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public When_SourceEndpointsAreRequested()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Then_GetSourcesReturnsFallbackConfiguration()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_tempDir, "docs")).FullName;
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = Path.Combine(_tempDir, "data", "sources.json"),
            ["KnowledgeDirs"] = docsDir,
        });
        var client = app.CreateClient();

        var response = await client.GetFromJsonAsync<SourceConfigurationFile>("/sources");

        response.ShouldNotBeNull();
        response.Sources.Count.ShouldBe(1);
        response.Sources[0].Kind.ShouldBe(SourceKind.Knowledge);
        response.Sources[0].HostPath.ShouldBe(Path.GetFullPath(docsDir));
        response.Sources[0].IndexDocs.ShouldBe(true);
        response.Sources[0].IndexCode.ShouldBe(false);
    }

    [Fact]
    public async Task Then_PutSourcesSavesIndentedJson()
    {
        var configPath = Path.Combine(_tempDir, "data", "sources.json");
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = configPath,
            ["KnowledgeDirs"] = Path.Combine(_tempDir, "fallback"),
        });
        var client = app.CreateClient();
        var source = SourceDefinition.FromConfiguredSource(ConfiguredSource.Create(
            "orders-ms",
            "orders-ms",
            SourceKind.Repository,
            Path.Combine(_tempDir, "orders-ms")));
        var payload = new SourceConfigurationFile(1, [source]);

        var saveResponse = await client.PutAsJsonAsync("/sources", payload);

        saveResponse.EnsureSuccessStatusCode();
        File.Exists(configPath).ShouldBeTrue();
        var saved = await File.ReadAllTextAsync(configPath);
        saved.ShouldContain(Environment.NewLine);
        saved.ShouldContain("\"id\": \"orders-ms\"");
        var loaded = await client.GetFromJsonAsync<SourceConfigurationFile>("/sources");
        loaded!.Sources.Single().Id.ShouldBe("orders-ms");
    }

    [Fact]
    public async Task Then_PutSourcesRejectsDuplicateIds()
    {
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = Path.Combine(_tempDir, "sources.json"),
        });
        var client = app.CreateClient();
        var path = Path.Combine(_tempDir, "repo");
        var payload = new SourceConfigurationFile(1,
        [
            SourceDefinition.FromConfiguredSource(ConfiguredSource.Create("repo", "Repo A", SourceKind.Repository, path)),
            SourceDefinition.FromConfiguredSource(ConfiguredSource.Create("repo", "Repo B", SourceKind.Repository, path)),
        ]);

        var response = await client.PutAsJsonAsync("/sources", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResult>();
        error!.Error.ShouldContain("duplicado");
    }

    [Fact]
    public async Task Then_ExportReturnsGeneratedFallbackWhenFileDoesNotExist()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_tempDir, "docs")).FullName;
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = Path.Combine(_tempDir, "missing", "sources.json"),
            ["KnowledgeDirs"] = docsDir,
        });
        var client = app.CreateClient();

        var exported = await client.GetStringAsync("/sources/export");

        exported.ShouldContain("\"version\"");
        exported.ShouldContain("docs");
    }

    [Fact]
    public async Task Then_RootsReflectSavedSourcesWithIndexDocs()
    {
        var docsPath = Path.Combine(_tempDir, "docs");
        var codeOnlyPath = Path.Combine(_tempDir, "code-only");
        using var app = CreateApp(new Dictionary<string, string?>
        {
            ["SourcesConfig"] = Path.Combine(_tempDir, "data", "sources.json"),
        });
        var client = app.CreateClient();
        var payload = new SourceConfigurationFile(1,
        [
            SourceDefinition.FromConfiguredSource(ConfiguredSource.Create("docs", "docs", SourceKind.Knowledge, docsPath)),
            SourceDefinition.FromConfiguredSource(ConfiguredSource.Create("code", "code", SourceKind.Repository, codeOnlyPath, indexDocs: false)),
        ]);
        (await client.PutAsJsonAsync("/sources", payload)).EnsureSuccessStatusCode();

        var roots = await client.GetFromJsonAsync<List<string>>("/roots");

        roots.ShouldBe(["docs"]);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private WebApplicationFactory<Program> CreateApp(Dictionary<string, string?> settings)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(settings);
                });
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ILogService>(_ => new LogService(Path.Combine(_tempDir, "test.log")));
                });
            });
    }
}
