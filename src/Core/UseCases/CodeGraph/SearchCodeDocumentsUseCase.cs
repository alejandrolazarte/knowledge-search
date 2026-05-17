using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.CodeGraph;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record SearchCodeDocumentsCommand(
    string? Query,
    int? Limit,
    string? Modes,
    string? Repositories,
    string? Kinds);

public sealed record SearchCodeDocumentsResponse(IReadOnlyList<CodeDocumentSearchResponse> Results);

public sealed class SearchCodeDocumentsUseCase(ICodeGraphStore store)
    : IUseCase<SearchCodeDocumentsCommand, SearchCodeDocumentsResponse>
{
    public Task<Result<SearchCodeDocumentsResponse>> ExecuteAsync(
        SearchCodeDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Query))
        {
            return Task.FromResult<Result<SearchCodeDocumentsResponse>>(
                Result.Validation<SearchCodeDocumentsResponse>("El parámetro q es requerido."));
        }

        if (!Enum.TryParse(command.Modes ?? nameof(SearchMode.Default), ignoreCase: true, out SearchMode searchModes))
        {
            return Task.FromResult<Result<SearchCodeDocumentsResponse>>(
                Result.Validation<SearchCodeDocumentsResponse>($"modes inválido: '{command.Modes}'. Valores válidos: phrase, and, or"));
        }

        if (!TryParseKinds(command.Kinds, out var kindFilters))
        {
            return Task.FromResult<Result<SearchCodeDocumentsResponse>>(
                Result.Validation<SearchCodeDocumentsResponse>($"kinds inválido: '{command.Kinds}'. Valores válidos: Class, Interface, Record, Enum, Method"));
        }

        var repositoryFilters = command.Repositories?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var results = store.SearchCodeDocuments(
            command.Query,
            Math.Clamp(command.Limit ?? 10, 1, 100),
            searchModes,
            repositoryFilters,
            kindFilters);

        return Task.FromResult(results.Resolve<Result<SearchCodeDocumentsResponse>>(
            documents => new SearchCodeDocumentsResponse(documents.Select(CodeDocumentSearchResponse.From).ToList()),
            error => error));
    }

    private static bool TryParseKinds(string? kinds, out IReadOnlyList<CodeNodeKind>? kindFilters)
    {
        kindFilters = null;
        if (kinds is null)
        {
            return true;
        }

        var parsed = new List<CodeNodeKind>();
        foreach (var kind in kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse(kind, ignoreCase: true, out CodeNodeKind parsedKind))
            {
                return false;
            }

            parsed.Add(parsedKind);
        }

        kindFilters = parsed;
        return true;
    }
}
