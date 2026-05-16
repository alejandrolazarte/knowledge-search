using KnowledgeSearch;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_SourceConfigurationResolves : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public When_SourceConfigurationResolves()
    {
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Then_SourcesJsonOverridesKnowledgeDirs()
    {
        var repositoryDir = Directory.CreateDirectory(Path.Combine(_tempDir, "repo")).FullName;
        File.WriteAllText(Path.Combine(_tempDir, "sources.json"), $$"""
        {
          "version": 1,
          "sources": [
            {
              "id": "repo",
              "name": "repo",
              "kind": "repository",
              "hostPath": "{{JsonPath(repositoryDir)}}"
            }
          ]
        }
        """);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KnowledgeDirs"] = Path.Combine(_tempDir, "legacy"),
                ["SourcesConfig"] = Path.Combine(_tempDir, "sources.json"),
            })
            .Build();

        var sources = AppConfiguration.ResolveSources(configuration, _ => null);

        sources.KnowledgeRoots.ShouldBe([Path.GetFullPath(repositoryDir)]);
        sources.Sources.Single().Kind.ShouldBe(SourceKind.Repository);
        sources.Sources.Single().IndexCode.ShouldBeTrue();
        sources.Sources.Single().IndexDocs.ShouldBeTrue();
    }

    [Fact]
    public void Then_KnowledgeDirsFallbackCreatesKnowledgeSources()
    {
        var first = Directory.CreateDirectory(Path.Combine(_tempDir, "docs-a")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_tempDir, "docs-b")).FullName;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourcesConfig"] = Path.Combine(_tempDir, "missing.json"),
                ["KnowledgeDirs"] = $"{first};{second}",
            })
            .Build();

        var sources = AppConfiguration.ResolveSources(configuration, _ => null);

        sources.KnowledgeRoots.ShouldBe([Path.GetFullPath(first), Path.GetFullPath(second)]);
        sources.Sources.Select(source => source.Kind).ShouldAllBe(kind => kind == SourceKind.Knowledge);
        sources.Sources.ShouldAllBe(source => source.IndexDocs && !source.IndexCode);
    }

    [Fact]
    public void Then_RepositorySourcesUseDefaultIncludesAndExcludes()
    {
        var repositoryDir = Directory.CreateDirectory(Path.Combine(_tempDir, "orders-ms")).FullName;
        File.WriteAllText(Path.Combine(_tempDir, "sources.json"), $$"""
        {
          "version": 1,
          "sources": [
            {
              "id": "orders-ms",
              "name": "orders-ms",
              "kind": "repository",
              "hostPath": "{{JsonPath(repositoryDir)}}"
            }
          ]
        }
        """);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourcesConfig"] = Path.Combine(_tempDir, "sources.json"),
            })
            .Build();

        var source = AppConfiguration.ResolveSources(configuration, _ => null).Sources.Single();

        source.DocIncludes.ShouldContain("README.md");
        source.DocIncludes.ShouldContain("docs/**/*.md");
        source.DocIncludes.ShouldContain("specs/**/*.md");
        source.Excludes.ShouldContain("**/node_modules/**");
        source.Excludes.ShouldContain("**/obj/**");
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static string JsonPath(string path) => path.Replace("\\", "\\\\");
}
