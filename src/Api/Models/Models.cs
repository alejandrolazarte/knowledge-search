using System.Text.Json.Serialization;
using KnowledgeSearch.Core.Domain.Search;
using KnowledgeSearch.Core.Domain.Sources;
using KnowledgeSearch.Core.UseCases.Search;

namespace KnowledgeSearch;

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
[JsonSerializable(typeof(List<SourceDefinition>))]
[JsonSerializable(typeof(ConfiguredSource))]
[JsonSerializable(typeof(List<ConfiguredSource>))]
[JsonSerializable(typeof(LogEvent))]
[JsonSerializable(typeof(IndexResult))]
[JsonSerializable(typeof(HealthResult))]
[JsonSerializable(typeof(SaveDocumentFileResponse))]
[JsonSerializable(typeof(ErrorResult))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(ScanDirectoryRequest))]
[JsonSerializable(typeof(ScanRepositoryResponse))]
[JsonSerializable(typeof(CodeGraphResponse))]
[JsonSerializable(typeof(List<CodeNodeResponse>))]
[JsonSerializable(typeof(List<CodeEdgeResponse>))]
[JsonSerializable(typeof(CodeSubgraphResponse))]
[JsonSerializable(typeof(List<CodeSearchNodeResponse>))]
[JsonSerializable(typeof(CrossRepoSubgraphResponse))]
[JsonSerializable(typeof(List<CrossRepoSearchNodeResponse>))]
[JsonSerializable(typeof(List<CrossRepoSearchEdgeResponse>))]
[JsonSerializable(typeof(List<CrossRepoLinkResponse>))]
[JsonSerializable(typeof(CrossRefSummaryResponse))]
[JsonSerializable(typeof(List<CodeDocumentSearchResponse>))]
internal partial class AppJsonContext : JsonSerializerContext { }
