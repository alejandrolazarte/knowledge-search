using System.Text.Json.Serialization;

namespace KnowledgeSearch;

internal record SearchResult(string Title, string Section, string Path, int Line, string Content, string Root);
internal record IndexResult(int Added, int Updated, int Deleted);
internal record HealthResult(string Status);
internal record ErrorResult(string Error);
internal record SkillSummary(string Name, string Description, string DirName, string FilePath);

public record LogEvent(
    [property: JsonPropertyName("ts")] string Ts,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("path")] string Path);

[JsonSerializable(typeof(List<SearchResult>))]
[JsonSerializable(typeof(List<SkillSummary>))]
[JsonSerializable(typeof(List<LogEvent>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(SourceConfigurationFile))]
[JsonSerializable(typeof(SourceDefinition))]
[JsonSerializable(typeof(ConfiguredSource))]
[JsonSerializable(typeof(List<ConfiguredSource>))]
[JsonSerializable(typeof(LogEvent))]
[JsonSerializable(typeof(IndexResult))]
[JsonSerializable(typeof(HealthResult))]
[JsonSerializable(typeof(ErrorResult))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(ScanDirectoryRequest))]
[JsonSerializable(typeof(ScanSummaryApiResponse))]
[JsonSerializable(typeof(CodeGraphApiResponse))]
[JsonSerializable(typeof(List<CodeNodeApiResponse>))]
[JsonSerializable(typeof(List<CodeEdgeApiResponse>))]
[JsonSerializable(typeof(CodeSubgraphApiResponse))]
[JsonSerializable(typeof(List<CodeSearchNodeApiResponse>))]
[JsonSerializable(typeof(CrossRepoSubgraphApiResponse))]
[JsonSerializable(typeof(List<CrossRepoSearchNodeApiResponse>))]
[JsonSerializable(typeof(List<CrossRepoSearchEdgeApiResponse>))]
[JsonSerializable(typeof(List<CrossRepoLinkApiResponse>))]
[JsonSerializable(typeof(CrossRefSummaryApiResponse))]
[JsonSerializable(typeof(List<CodeDocumentSearchApiResponse>))]
internal partial class AppJsonContext : JsonSerializerContext { }
