using System.Text.Json.Serialization;

namespace KnowledgeSearch.Core.Domain.Sources;

[JsonConverter(typeof(JsonStringEnumConverter<SourceKind>))]
public enum SourceKind
{
    Knowledge,
    Repository,
}
