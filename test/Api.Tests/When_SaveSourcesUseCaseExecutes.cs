using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Abstractions.Sources;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Sources;
using KnowledgeSearch.Core.UseCases.Sources;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_SaveSourcesUseCaseExecutes
{
    [Fact]
    public async Task Then_ItUpdatesDocRootsAndScansCodeSources()
    {
        var configuration = new SourceConfigurationFile(1, [
            new SourceDefinition(
                "docs",
                "docs",
                SourceKind.Knowledge,
                @"D:\Docs",
                IndexCode: false,
                IndexDocs: true,
                DocIncludes: null,
                CodeIncludes: null,
                Excludes: null),
            new SourceDefinition(
                "repo",
                "repo",
                SourceKind.Repository,
                @"D:\Repo",
                IndexCode: true,
                IndexDocs: false,
                DocIncludes: null,
                CodeIncludes: null,
                Excludes: null),
        ]);

        var store = new Mock<ISourceConfigurationStore>();
        var documentIndex = new Mock<IDocumentIndex>();
        var codeScanner = new Mock<ICodeGraphScanner>();
        var fileSystem = new Mock<IFileSystem>();

        var expectedDocsPath = ConfiguredSource.ToAccessiblePath("docs", @"D:\Docs");
        var expectedRepoPath = ConfiguredSource.ToAccessiblePath("repo", @"D:\Repo");

        store.Setup(service => service.Save(configuration)).Returns(Result.Success());
        store.Setup(service => service.GetConfiguration()).Returns(configuration);
        fileSystem.Setup(system => system.DirectoryExists(expectedRepoPath)).Returns(true);

        var useCase = new SaveSourcesUseCase(
            store.Object,
            documentIndex.Object,
            codeScanner.Object,
            fileSystem.Object);

        var result = await useCase.ExecuteAsync(new SaveSourcesCommand(configuration), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Configuration.ShouldBe(configuration);
        documentIndex.Verify(index => index.UpdateRoots(It.Is<IReadOnlyList<string>>(roots =>
            roots.Count == 1 && roots[0] == expectedDocsPath)), Times.Once);
        codeScanner.Verify(scanner => scanner.ScanDirectory(expectedRepoPath), Times.Once);
    }

    [Fact]
    public async Task Then_ItReturnsValidationFailureFromStore()
    {
        var configuration = new SourceConfigurationFile(1, []);
        var store = new Mock<ISourceConfigurationStore>();
        var documentIndex = new Mock<IDocumentIndex>();
        var codeScanner = new Mock<ICodeGraphScanner>();
        var fileSystem = new Mock<IFileSystem>();

        store.Setup(service => service.Save(configuration))
            .Returns(Result.Validation("id duplicado: docs"));

        var useCase = new SaveSourcesUseCase(
            store.Object,
            documentIndex.Object,
            codeScanner.Object,
            fileSystem.Object);

        var result = await useCase.ExecuteAsync(new SaveSourcesCommand(configuration), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Message.ShouldBe("id duplicado: docs");
        documentIndex.Verify(index => index.UpdateRoots(It.IsAny<IReadOnlyList<string>>()), Times.Never);
        codeScanner.Verify(scanner => scanner.ScanDirectory(It.IsAny<string>()), Times.Never);
    }
}
