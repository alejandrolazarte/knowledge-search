using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases.Search;

public sealed record SaveDocumentFileCommand(string? Path, string Content);

public sealed record SaveDocumentFileResponse(bool Saved, string Path);

public sealed class SaveDocumentFileUseCase(
    IDocumentSearchIndex searchIndex,
    IFileSystem fileSystem)
    : IUseCase<SaveDocumentFileCommand, SaveDocumentFileResponse>
{
    public async Task<Result<SaveDocumentFileResponse>> ExecuteAsync(
        SaveDocumentFileCommand command,
        CancellationToken cancellationToken)
    {
        var pathResult = ResolveAllowedExistingPath(command.Path);
        if (pathResult.IsFailure)
        {
            return pathResult.Error!;
        }

        await fileSystem.WriteAllTextAsync(pathResult.Value!, command.Content, cancellationToken);
        return new SaveDocumentFileResponse(true, pathResult.Value!);
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
