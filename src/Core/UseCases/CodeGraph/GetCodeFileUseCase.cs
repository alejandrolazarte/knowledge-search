using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Abstractions.Files;
using KnowledgeSearch.Core.Common;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record GetCodeFileCommand(string? Path);

public sealed record GetCodeFileResponse(string Content);

public sealed class GetCodeFileUseCase(
    ICodeGraphStore store,
    IFileSystem fileSystem)
    : IUseCase<GetCodeFileCommand, GetCodeFileResponse>
{
    public async Task<Result<GetCodeFileResponse>> ExecuteAsync(
        GetCodeFileCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Path))
        {
            return Result.Validation<GetCodeFileResponse>("Falta parámetro path");
        }

        var fullPath = fileSystem.GetFullPath(command.Path);
        if (!store.IsCodePathAllowed(fullPath))
        {
            return Result.Validation<GetCodeFileResponse>("Ruta fuera de los repos escaneados");
        }

        if (!fileSystem.FileExists(fullPath))
        {
            return Result.NotFound<GetCodeFileResponse>("Archivo no encontrado");
        }

        return new GetCodeFileResponse(await fileSystem.ReadAllTextAsync(fullPath, cancellationToken));
    }
}
