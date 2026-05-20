using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Domain.Search;

namespace KnowledgeSearch;

internal interface IDbService : IDocumentIndex
{

    /// <summary>
    /// Busca documentos usando cascade phrase → AND → OR según <paramref name="modes"/>.
    /// Si <paramref name="roots"/> está vacío busca en todos los roots configurados.
    /// Retorna hasta <paramref name="limit"/> resultados ordenados por BM25.
    /// </summary>
    IReadOnlyList<SearchResult> Search(
        string query,
        int limit,
        SearchMode modes = SearchMode.Default,
        IReadOnlyList<string>? roots = null);

    /// <summary>
    /// Re-indexa todos los roots de forma incremental (solo archivos modificados).
    /// Usa una transacción por archivo para mayor performance.
    /// </summary>
    IndexResult IndexDirectories();

    /// <summary>
    /// Valida que el path esté dentro de alguno de los roots configurados.
    /// Usado por /file e /image para prevenir path traversal.
    /// </summary>
    bool IsPathAllowed(string fullPath);

    /// <summary>
    /// Re-indexa un archivo específico. Usado por WatcherService en cambios incrementales.
    /// </summary>
    void ReindexFile(string path);

    /// <summary>
    /// Elimina un archivo del índice. Usado por WatcherService cuando se detecta una eliminación.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Retorna los nombres (último segmento del path) de los roots configurados.
    /// </summary>
    IReadOnlyList<string> GetRootNames();

}
