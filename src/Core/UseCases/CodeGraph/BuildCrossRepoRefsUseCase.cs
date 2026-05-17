using KnowledgeSearch.Core.Abstractions.CodeGraph;
using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.CodeGraph;

namespace KnowledgeSearch.Core.UseCases.CodeGraph;

public sealed record BuildCrossRepoRefsCommand;

public sealed class BuildCrossRepoRefsUseCase(ICodeGraphSearchService graphService)
    : IUseCase<BuildCrossRepoRefsCommand, CrossRefSummaryResponse>
{
    public Task<Result<CrossRefSummaryResponse>> ExecuteAsync(
        BuildCrossRepoRefsCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<Result<CrossRefSummaryResponse>>(
            new CrossRefSummaryResponse(graphService.BuildCrossRepoEdges().Count));
    }
}
