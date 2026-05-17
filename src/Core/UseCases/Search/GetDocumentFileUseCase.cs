using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases.Search;

public sealed record GetDocumentFileCommand(string? Path);

public sealed record GetDocumentFileResponse(string Content);

public sealed class GetDocumentFileUseCase(
    IDocumentSearchIndex searchIndex,
    IFileSystem fileSystem)
    : IUseCase<GetDocumentFileCommand, GetDocumentFileResponse>
{
    public async Task<Result<GetDocumentFileResponse>> ExecuteAsync(
        GetDocumentFileCommand command,
        CancellationToken cancellationToken)
    {
        var pathResult = ResolveAllowedExistingPath(command.Path);
        if (pathResult.IsFailure)
        {
            return pathResult.Error!;
        }

        var content = await fileSystem.ReadAllTextAsync(pathResult.Value!, cancellationToken);
        return new GetDocumentFileResponse(content);
    }

    private Result<string> ResolveAllowedExistingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result.Validation<string>("Falta parámetro path");
        }

        var fullPath = fileSystem.GetFullPath(path);
        if (!searchIndex.IsPathAllowed(fullPath))
        {
            return Result.Validation<string>("Ruta fuera de los roots configurados");
        }

        return fileSystem.FileExists(fullPath)
            ? fullPath
            : Result.NotFound<string>("Archivo no encontrado");
    }
}
