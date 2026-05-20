using KnowledgeSearch.Core.Abstractions.Jobs;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.UseCases.Sources;

namespace KnowledgeSearch;

public sealed class IndexDocumentsJob(IDocumentSearchIndex searchIndex) : IJob
{
    public string Kind => "index-documents";

    public Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        var result = searchIndex.IndexDirectories();
        return context.ReportProgressAsync(
            $"markdown: added={result.Added} updated={result.Updated} deleted={result.Deleted}",
            cancellationToken);
    }
}

public sealed class IndexDocumentsJobFactory(IDocumentSearchIndex searchIndex) : IIndexDocumentsJobFactory
{
    public IJob Create() => new IndexDocumentsJob(searchIndex);
}
