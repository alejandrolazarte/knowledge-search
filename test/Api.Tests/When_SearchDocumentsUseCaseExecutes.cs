using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Search;
using KnowledgeSearch.Core.UseCases.Search;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_SearchDocumentsUseCaseExecutes
{
    [Fact]
    public async Task Then_BlankQueryReturnsValidation()
    {
        var index = new Mock<IDocumentSearchIndex>();
        var useCase = new SearchDocumentsUseCase(index.Object);

        var result = await useCase.ExecuteAsync(
            new SearchDocumentsCommand("", 5, null, null),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Message.ShouldBe("Falta parámetro q");
    }

    [Fact]
    public async Task Then_InvalidModesReturnsValidation()
    {
        var index = new Mock<IDocumentSearchIndex>();
        var useCase = new SearchDocumentsUseCase(index.Object);

        var result = await useCase.ExecuteAsync(
            new SearchDocumentsCommand("query", 5, "bad", null),
            CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldContain("modes inválido");
    }

    [Fact]
    public async Task Then_SearchesWithParsedModesAndRoots()
    {
        var expected = new[]
        {
            new SearchResult("Title", "Section", "path.md", 1, "content", "docs"),
        };
        var index = new Mock<IDocumentSearchIndex>();
        index.Setup(searchIndex => searchIndex.Search(
                "query",
                7,
                SearchMode.Phrase | SearchMode.Or,
                It.Is<IReadOnlyList<string>>(roots => roots.Count == 2 && roots[0] == "docs" && roots[1] == "specs")))
            .Returns(Result.Success<IReadOnlyList<SearchResult>>(expected));
        var useCase = new SearchDocumentsUseCase(index.Object);

        var result = await useCase.ExecuteAsync(
            new SearchDocumentsCommand("query", 7, "Phrase, Or", "docs,specs"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Results.ShouldBe(expected);
    }
}
