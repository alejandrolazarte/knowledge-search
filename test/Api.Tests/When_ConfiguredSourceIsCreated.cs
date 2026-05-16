using KnowledgeSearch.Core.Domain.Sources;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_ConfiguredSourceIsCreated
{
    [Fact]
    public void Then_RepositoryDefaultsIndexCodeAndDocs()
    {
        var source = ConfiguredSource.Create(
            id: "DevHub",
            name: "DevHub",
            kind: SourceKind.Repository,
            hostPath: @"D:\DevHub");

        source.IndexCode.ShouldBeTrue();
        source.IndexDocs.ShouldBeTrue();
        source.DocIncludes.ShouldContain("README.md");
        source.DocIncludes.ShouldContain("docs/**/*.md");
        source.Excludes.ShouldContain("**/node_modules/**");
    }

    [Fact]
    public void Then_KnowledgeDefaultsDocsOnly()
    {
        var source = ConfiguredSource.Create(
            id: "docs",
            name: "docs",
            kind: SourceKind.Knowledge,
            hostPath: @"D:\Documentation");

        source.IndexCode.ShouldBeFalse();
        source.IndexDocs.ShouldBeTrue();
        source.DocIncludes.ShouldBe(["**/*.md", "**/*.mdx"]);
    }

    [Fact]
    public void Then_SourceDefinitionNormalizesMissingIdAndName()
    {
        var definition = new SourceDefinition(
            Id: "",
            Name: "",
            Kind: SourceKind.Repository,
            HostPath: @"D:\DevHub",
            IndexCode: null,
            IndexDocs: null,
            DocIncludes: null,
            CodeIncludes: null,
            Excludes: null);

        var source = definition.ToConfiguredSource();

        source.Id.ShouldBe("DevHub");
        source.Name.ShouldBe("DevHub");
    }
}
