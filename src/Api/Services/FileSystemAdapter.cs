using KnowledgeSearch.Core.Abstractions.Files;

namespace KnowledgeSearch;

internal sealed class FileSystemAdapter : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
}
