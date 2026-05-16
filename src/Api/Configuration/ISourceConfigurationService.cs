namespace KnowledgeSearch;

internal interface ISourceConfigurationService
{
    SourceConfigurationFile GetConfiguration();
    IReadOnlyList<string> GetKnowledgeRootNames();
    string ExportJson();
    SaveSourcesResult Save(SourceConfigurationFile configuration);
}

internal sealed record SaveSourcesResult(bool Success, string? Error);
