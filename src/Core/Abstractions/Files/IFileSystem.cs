namespace KnowledgeSearch.Core.Abstractions.Files;

public interface IFileSystem
{
    bool DirectoryExists(string path);
}
