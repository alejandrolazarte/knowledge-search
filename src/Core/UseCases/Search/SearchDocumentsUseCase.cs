using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch.Core.UseCases.Search;

public sealed record SearchDocumentsCommand(
    string? Query,
    int Limit,
    string? Modes,
    string? Roots);

public sealed record SearchDocumentsResponse(IReadOnlyList<SearchResult> Results);

public sealed class SearchDocumentsUseCase(IDocumentSearchIndex searchIndex)
    : IUseCase<SearchDocumentsCommand, SearchDocumentsResponse>
{
    public Task<Result<SearchDocumentsResponse>> ExecuteAsync(
        SearchDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Query))
        {
            return Task.FromResult<Result<SearchDocumentsResponse>>(
                Result.Validation<SearchDocumentsResponse>("Falta parámetro q"));
        }

        if (!TryParseSearchModes(command.Modes, out var searchModes))
        {
            return Task.FromResult<Result<SearchDocumentsResponse>>(
                Result.Validation<SearchDocumentsResponse>(
                    $"modes inválido: '{command.Modes}'. Valores válidos: phrase, and, or"));
        }

        var rootFilter = command.Roots?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var result = searchIndex.Search(command.Query, command.Limit, searchModes, rootFilter);
        return Task.FromResult(result.Resolve<Result<SearchDocumentsResponse>>(
            results => new SearchDocumentsResponse(results),
            error => error));
    }

    private static bool TryParseSearchModes(string? modes, out SearchMode searchModes)
    {
        if (modes is null)
        {
            searchModes = SearchMode.Default;
            return true;
        }

        return Enum.TryParse(modes, ignoreCase: true, out searchModes);
    }
}
