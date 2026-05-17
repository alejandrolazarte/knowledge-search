using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.UseCases.CodeGraph;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_CodeGraphUseCasesExecute
{
    [Fact]
    public async Task Then_GetRepositoryGraphReturnsNotFoundWhenRepositoryIsMissing()
    {
        var store = new Mock<ICodeGraphStore>();
        var useCase = new GetRepositoryGraphUseCase(store.Object);

        var result = await useCase.ExecuteAsync(new GetRepositoryGraphCommand("missing"), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Kind.ShouldBe(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Then_ScanRepositoryValidatesDirectory()
    {
        var service = new Mock<ICodeGraphSearchService>();
        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(system => system.DirectoryExists(@"D:\Repo")).Returns(false);
        var useCase = new ScanRepositoryUseCase(service.Object, fileSystem.Object);

        var result = await useCase.ExecuteAsync(new ScanRepositoryCommand(@"D:\Repo"), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldContain("El directorio no existe");
    }

    [Fact]
    public async Task Then_SearchCodeDocumentsParsesFilters()
    {
        var store = new Mock<ICodeGraphStore>();
        store.Setup(repository => repository.SearchCodeDocuments(
                "Needle",
                10,
                SearchMode.Default,
                It.Is<IReadOnlyList<string>?>(repositories => repositories != null && repositories[0] == "repo"),
                It.Is<IReadOnlyList<CodeNodeKind>?>(kinds => kinds != null && kinds[0] == CodeNodeKind.Class)))
            .Returns(Result.Success<IReadOnlyList<CodeDocumentSearchResult>>([
                new("repo", "UserService", "UserService", CodeNodeKind.Class, "file.cs", 1, "Needle", -1),
            ]));
        var useCase = new SearchCodeDocumentsUseCase(store.Object);

        var result = await useCase.ExecuteAsync(
            new SearchCodeDocumentsCommand("Needle", null, null, "repo", "Class"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Results.ShouldHaveSingleItem();
        result.Value.Results[0].Kind.ShouldBe("Class");
    }

    [Fact]
    public async Task Then_SearchRepositorySubgraphValidatesRepository()
    {
        var store = new Mock<ICodeGraphStore>();
        var service = new Mock<ICodeGraphSearchService>();
        var useCase = new SearchRepositorySubgraphUseCase(store.Object, service.Object);

        var result = await useCase.ExecuteAsync(
            new SearchRepositorySubgraphCommand("missing", "Service", 2),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Kind.ShouldBe(ErrorKind.NotFound);
    }
}
