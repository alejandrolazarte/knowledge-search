using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch.Core.UseCases.Search;

public sealed record IndexDocumentsCommand;

public sealed record IndexDocumentsResponse(IndexResult Result);

public sealed class IndexDocumentsUseCase(IDocumentSearchIndex searchIndex)
    : IUseCase<IndexDocumentsCommand, IndexDocumentsResponse>
{
    public Task<Result<IndexDocumentsResponse>> ExecuteAsync(
        IndexDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Result<IndexDocumentsResponse>>(
            new IndexDocumentsResponse(searchIndex.IndexDirectories()));
    }
}
