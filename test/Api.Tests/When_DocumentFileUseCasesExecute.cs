using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.UseCases.Search;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_DocumentFileUseCasesExecute
{
    [Fact]
    public async Task Then_GetFileRejectsOutsideRoot()
    {
        var (index, files) = CreateDependencies(pathAllowed: false, fileExists: true);
        var useCase = new GetDocumentFileUseCase(index.Object, files.Object);

        var result = await useCase.ExecuteAsync(new GetDocumentFileCommand("notes.md"), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldBe("Ruta fuera de los roots configurados");
    }

    [Fact]
    public async Task Then_GetFileReturnsContent()
    {
        var (index, files) = CreateDependencies(pathAllowed: true, fileExists: true);
        files.Setup(system => system.ReadAllTextAsync(@"D:\Docs\notes.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync("hello");
        var useCase = new GetDocumentFileUseCase(index.Object, files.Object);

        var result = await useCase.ExecuteAsync(new GetDocumentFileCommand("notes.md"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Content.ShouldBe("hello");
    }

    [Fact]
    public async Task Then_SaveFileWritesContent()
    {
        var (index, files) = CreateDependencies(pathAllowed: true, fileExists: true);
        var useCase = new SaveDocumentFileUseCase(index.Object, files.Object);

        var result = await useCase.ExecuteAsync(
            new SaveDocumentFileCommand("notes.md", "updated"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Saved.ShouldBeTrue();
        result.Value.Path.ShouldBe(@"D:\Docs\notes.md");
        files.Verify(system => system.WriteAllTextAsync(
            @"D:\Docs\notes.md",
            "updated",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Then_ImageReturnsContentType()
    {
        var (index, files) = CreateDependencies(pathAllowed: true, fileExists: true, fullPath: @"D:\Docs\image.webp");
        files.Setup(system => system.ReadAllBytesAsync(@"D:\Docs\image.webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2, 3]);
        var useCase = new GetImageFileUseCase(index.Object, files.Object);

        var result = await useCase.ExecuteAsync(new GetImageFileCommand("image.webp"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.ContentType.ShouldBe("image/webp");
        result.Value.Content.ShouldBe([1, 2, 3]);
    }

    private static (Mock<IDocumentSearchIndex> Index, Mock<IFileSystem> Files) CreateDependencies(
        bool pathAllowed,
        bool fileExists,
        string fullPath = @"D:\Docs\notes.md")
    {
        var index = new Mock<IDocumentSearchIndex>();
        var files = new Mock<IFileSystem>();
        files.Setup(system => system.GetFullPath(It.IsAny<string>())).Returns(fullPath);
        files.Setup(system => system.FileExists(fullPath)).Returns(fileExists);
        index.Setup(searchIndex => searchIndex.IsPathAllowed(fullPath)).Returns(pathAllowed);
        return (index, files);
    }
}
