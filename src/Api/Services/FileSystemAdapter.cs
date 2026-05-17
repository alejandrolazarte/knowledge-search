using KnowledgeSearch.Core.Abstractions.Files;

namespace KnowledgeSearch;

internal sealed class FileSystemAdapter : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public bool FileExists(string path) => File.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(path, content, cancellationToken);

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllBytesAsync(path, cancellationToken);
}
