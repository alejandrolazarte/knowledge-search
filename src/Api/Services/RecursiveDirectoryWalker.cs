using Microsoft.Extensions.FileSystemGlobbing;

namespace KnowledgeSearch;

/// <summary>
/// Configuracion del recorrido. <paramref name="IncludeExtensions"/> vacio devuelve
/// todos los ficheros del arbol no excluido. <paramref name="ExcludedDirectoryNames"/>
/// son nombres exactos a cortar (p.ej. <c>node_modules</c>). <paramref name="ExcludeGlobs"/>
/// son patrones glob relativos al root.
/// </summary>
public sealed record DirectoryWalkOptions(
    IReadOnlyList<string> IncludeExtensions,
    IReadOnlyList<string> ExcludedDirectoryNames,
    IReadOnlyList<string> ExcludeGlobs);

public sealed record DirectoryWalkResult(
    IReadOnlyList<string> Files,
    long DirectoriesDescended);

/// <summary>
/// Walker recursivo que omite ramas excluidas antes de descender, a diferencia
/// de <c>Directory.EnumerateFiles(..., AllDirectories)</c>, que entra a las
/// ramas y descarta despues.
/// </summary>
public sealed class RecursiveDirectoryWalker
{
    public DirectoryWalkResult Enumerate(string root, DirectoryWalkOptions options)
    {
        if (!Directory.Exists(root))
        {
            return new DirectoryWalkResult([], 0);
        }

        var excludedNames = new HashSet<string>(options.ExcludedDirectoryNames, StringComparer.OrdinalIgnoreCase);
        var matcher = BuildExcludeMatcher(options.ExcludeGlobs);
        var includeExtensions = new HashSet<string>(options.IncludeExtensions, StringComparer.OrdinalIgnoreCase);
        var filterByExtension = includeExtensions.Count > 0;

        var files = new List<string>();
        long descended = 0;
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            descended++;

            IEnumerable<string> filesInDir;
            IEnumerable<string> subDirs;
            try
            {
                filesInDir = Directory.EnumerateFiles(currentDir);
                subDirs    = Directory.EnumerateDirectories(currentDir);
            }
            catch (DirectoryNotFoundException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var filePath in filesInDir)
            {
                if (filterByExtension && !includeExtensions.Contains(Path.GetExtension(filePath)))
                {
                    continue;
                }
                if (IsExcludedByGlob(matcher, root, filePath))
                {
                    continue;
                }
                files.Add(filePath);
            }

            foreach (var subDir in subDirs)
            {
                var name = Path.GetFileName(subDir);
                if (excludedNames.Contains(name))
                {
                    continue;
                }
                if (IsExcludedByGlob(matcher, root, subDir))
                {
                    continue;
                }
                stack.Push(subDir);
            }
        }

        return new DirectoryWalkResult(files, descended);
    }

    private static Matcher? BuildExcludeMatcher(IReadOnlyList<string> excludeGlobs)
    {
        if (excludeGlobs.Count == 0)
        {
            return null;
        }
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude("**/*");
        matcher.AddExcludePatterns(excludeGlobs);
        return matcher;
    }

    private static bool IsExcludedByGlob(Matcher? matcher, string root, string path)
    {
        if (matcher is null)
        {
            return false;
        }
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return !matcher.Match(relative).HasMatches;
    }
}
