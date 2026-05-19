using Microsoft.Extensions.FileSystemGlobbing;

namespace KnowledgeSearch;

// Opciones para Enumerate.
//  - IncludeExtensions: lista de extensiones (con punto) — si esta vacia,
//    se devuelven todos los ficheros del arbol no excluido.
//  - ExcludedDirectoryNames: nombres exactos de carpetas a cortar (p.ej.
//    "node_modules", "bin"). El walker NO baja a estas.
//  - ExcludeGlobs: patrones glob relativos al root (p.ej. "**/sandbox/**"). Si
//    el path relativo de un directorio o fichero matchea, se descarta.
public sealed record DirectoryWalkOptions(
    IReadOnlyList<string> IncludeExtensions,
    IReadOnlyList<string> ExcludedDirectoryNames,
    IReadOnlyList<string> ExcludeGlobs);

public sealed record DirectoryWalkResult(
    IReadOnlyList<string> Files,
    long DirectoriesDescended);

// Walker recursivo que CORTA la rama antes de descender — a diferencia de
// Directory.EnumerateFiles(..., AllDirectories), que entra a node_modules
// y descarta despues.
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
