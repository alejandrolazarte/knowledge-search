using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases.Search;

public sealed record GetImageFileCommand(string? Path);

public sealed record GetImageFileResponse(byte[] Content, string ContentType);

public sealed class GetImageFileUseCase(
    IDocumentSearchIndex searchIndex,
    IFileSystem fileSystem)
    : IUseCase<GetImageFileCommand, GetImageFileResponse>
{
    public async Task<Result<GetImageFileResponse>> ExecuteAsync(
        GetImageFileCommand command,
        CancellationToken cancellationToken)
    {
        var pathResult = ResolveAllowedExistingPath(command.Path);
        if (pathResult.IsFailure)
        {
            return pathResult.Error!;
        }

        var content = await fileSystem.ReadAllBytesAsync(pathResult.Value!, cancellationToken);
        return new GetImageFileResponse(content, GetContentType(pathResult.Value!));
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

    private static string GetContentType(string fullPath) =>
        Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
}
