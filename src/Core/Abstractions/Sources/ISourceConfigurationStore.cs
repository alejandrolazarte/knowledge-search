using KnowledgeSearch.Core.Common;
using KnowledgeSearch.Core.Domain.Sources;

namespace KnowledgeSearch.Core.Abstractions.Sources;

public interface ISourceConfigurationStore
{
    SourceConfigurationFile GetConfiguration();
    IReadOnlyList<string> GetKnowledgeRootNames();
    string ExportJson();
    Result Save(SourceConfigurationFile configuration);
}
