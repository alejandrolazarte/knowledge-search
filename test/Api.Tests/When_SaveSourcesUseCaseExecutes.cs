using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Jobs;
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
    public async Task Then_ItUpdatesDocRootsAndEnqueuesJobs()
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
        var jobQueue = new Mock<IJobQueue>();
        var indexJobFactory = new Mock<IIndexDocumentsJobFactory>();
        var scanJobFactory = new Mock<IScanRepositoryJobFactory>();
        var fileSystem = new Mock<IFileSystem>();

        var expectedDocsPath = ConfiguredSource.ToAccessiblePath("docs", @"D:\Docs");
        var expectedRepoPath = ConfiguredSource.ToAccessiblePath("repo", @"D:\Repo");
        var indexJob = new Mock<IJob>();
        var scanJob  = new Mock<IJob>();

        store.Setup(s => s.Save(configuration)).Returns(Result.Success());
        store.Setup(s => s.GetConfiguration()).Returns(configuration);
        fileSystem.Setup(system => system.DirectoryExists(expectedRepoPath)).Returns(true);
        indexJobFactory.Setup(f => f.Create()).Returns(indexJob.Object);
        scanJobFactory.Setup(f => f.Create(expectedRepoPath)).Returns(scanJob.Object);
        jobQueue.Setup(q => q.EnqueueAsync(It.IsAny<IJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var useCase = new SaveSourcesUseCase(
            store.Object,
            documentIndex.Object,
            jobQueue.Object,
            indexJobFactory.Object,
            scanJobFactory.Object,
            fileSystem.Object);

        var result = await useCase.ExecuteAsync(new SaveSourcesCommand(configuration), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Configuration.ShouldBe(configuration);
        result.Value.JobIds.Count.ShouldBe(2);
        documentIndex.Verify(index => index.UpdateSources(It.Is<IReadOnlyList<ConfiguredSource>>(sources =>
            sources.Count == 1 && sources[0].GetAccessiblePath() == expectedDocsPath)), Times.Once);
        jobQueue.Verify(q => q.EnqueueAsync(indexJob.Object, It.IsAny<CancellationToken>()), Times.Once);
        jobQueue.Verify(q => q.EnqueueAsync(scanJob.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Then_ItReturnsValidationFailureFromStoreWithoutEnqueuing()
    {
        var configuration = new SourceConfigurationFile(1, []);
        var store = new Mock<ISourceConfigurationStore>();
        var documentIndex = new Mock<IDocumentIndex>();
        var jobQueue = new Mock<IJobQueue>();
        var indexJobFactory = new Mock<IIndexDocumentsJobFactory>();
        var scanJobFactory = new Mock<IScanRepositoryJobFactory>();
        var fileSystem = new Mock<IFileSystem>();

        store.Setup(s => s.Save(configuration)).Returns(Result.Validation("id duplicado: docs"));

        var useCase = new SaveSourcesUseCase(
            store.Object,
            documentIndex.Object,
            jobQueue.Object,
            indexJobFactory.Object,
            scanJobFactory.Object,
            fileSystem.Object);

        var result = await useCase.ExecuteAsync(new SaveSourcesCommand(configuration), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldBe("id duplicado: docs");
        documentIndex.Verify(index => index.UpdateSources(It.IsAny<IReadOnlyList<ConfiguredSource>>()), Times.Never);
        jobQueue.Verify(q => q.EnqueueAsync(It.IsAny<IJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
