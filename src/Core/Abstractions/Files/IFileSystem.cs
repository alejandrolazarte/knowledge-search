namespace KnowledgeSearch.Core.Abstractions.Files;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    string GetFullPath(string path);
    bool FileExists(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken);
}
