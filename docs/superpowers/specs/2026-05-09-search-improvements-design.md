# Search Improvements + Multiple Roots — Design Spec

**Fecha:** 2026-05-09  
**Estado:** Aprobado

---

## Objetivo

Tres mejoras relacionadas al buscador y la infraestructura de indexado:

1. **Refactor `DbService`** — de static a instancia singleton (`IDbService`), con SQL encapsulado como constantes privadas y documentación XML en la interface.
2. **`BuildFtsQuery` más inteligente** — cascade phrase → AND → OR para que el match exacto de frase rankee primero.
3. **Múltiples roots de documentación** — `KnowledgeDirs` acepta varias carpetas separadas por `;`. Los resultados muestran de qué root vienen.
4. **Transacciones en el indexado** — cada archivo se indexa dentro de una transacción para reducir I/O (~10x más rápido para repos grandes).
5. **Middleware global de excepciones** — reemplaza los try/catch dispersos.
6. **`IConfiguration`** — reemplaza `AppConfig` static.

---

## Modelos

### `SearchResult` — agrega campo `Root`

```csharp
internal record SearchResult(
    string Title,
    string Section,
    string Path,
    int Line,
    string Content,
    string Root);
```

`Root` es el último segmento del directorio raíz (`Path.GetFileName(root)`), por ejemplo `"Documentation"` o `"proyecto-x"`. Se deriva en el momento de la búsqueda, no se persiste en la DB.

### `ErrorResult` — nuevo

```csharp
internal record ErrorResult(string Error);
```

Devuelto por el middleware global en respuestas 500.

---

## Configuración — `IConfiguration` reemplaza `AppConfig`

`AppConfig.cs` se elimina. La configuración se lee directamente en `Program.cs` desde `builder.Configuration`, que mergea `appsettings.json` y variables de entorno automáticamente.

**`appsettings.json`:**
```json
{
  "KnowledgeDb":   "D:\\Documentation\\knowledge.db",
  "KnowledgeDirs": "D:\\Documentation",
  "SkillsDir":     "C:\\Users\\Alejandro\\.claude\\skills"
}
```

**Múltiples roots:**
```json
"KnowledgeDirs": "D:\\Documentation;D:\\work\\repos"
```

**`Program.cs`:**
```csharp
var dbPath = builder.Configuration["KnowledgeDb"]
    ?? Path.GetFullPath("../knowledge.db");

var roots = (builder.Configuration["KnowledgeDirs"] ?? string.Empty)
    .Split(';', StringSplitOptions.RemoveEmptyEntries)
    .Select(Path.GetFullPath)
    .ToArray();

var skillsDir = builder.Configuration["SkillsDir"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "skills");
```

---

## `IDbService` — nueva interface

```csharp
internal interface IDbService
{
    /// <summary>
    /// Busca documentos en el índice FTS5 usando cascade phrase → AND → OR.
    /// Retorna hasta <paramref name="limit"/> resultados ordenados por BM25.
    /// </summary>
    IReadOnlyList<SearchResult> Search(string query, int limit);

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
}
```

---

## `DbService` — instancia singleton

```csharp
internal sealed class DbService : IDbService
{
    private readonly SqliteConnection _connection;
    private readonly IReadOnlyList<string> _roots;

    private const string SearchSql =
        "SELECT title, section, path, line, content " +
        "FROM docs WHERE docs MATCH @query " +
        "ORDER BY bm25(docs, 10, 5, 1) LIMIT @limit";

    public DbService(string dbPath, IReadOnlyList<string> roots)
    {
        _roots = roots;
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        ApplyPragmas();
        EnsureSchema();
    }
}
```

### `BuildFtsQuery` — cascade phrase → AND → OR

Para consultas de una palabra: igual que hoy (`"term"`).  
Para múltiples palabras:

```
"high cohesion" OR (high AND cohesion) OR high OR cohesion
```

BM25 rankea automáticamente el match exacto de frase primero, luego AND, luego OR suelto.

### Transacciones en indexado

```csharp
using var transaction = _connection.BeginTransaction();
// INSERTs de todas las secciones del archivo
transaction.Commit();
```

Una transacción por archivo. Para ~2300 archivos (77 repos × 30 archivos promedio) se espera reducir el tiempo de indexado inicial de ~90s a ~10-15s.

### `IsPathAllowed`

```csharp
/// <inheritdoc/>
public bool IsPathAllowed(string fullPath) =>
    _roots.Any(root =>
        fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
```

---

## `WatcherService` — múltiples roots

Recibe `IReadOnlyList<string> roots` y crea un `FileSystemWatcher` por cada root. Si un watcher falla en runtime (directorio eliminado), loguea y continúa — los otros watchers no se ven afectados.

---

## Middleware global de excepciones

Registrado en `Program.cs` antes de mapear rutas:

```csharp
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    var exception = feature?.Error;

    logger.LogError(exception, "Unhandled exception on {Method} {Path}",
        context.Request.Method, context.Request.Path);

    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";

    await context.Response.WriteAsJsonAsync(
        new ErrorResult("Error interno del servidor. Revisá los logs para más detalles."));
}));
```

Los servicios lanzan excepciones naturalmente — sin try/catch internos.

---

## Registro en DI

```csharp
// Program.cs
var dbService = new DbService(dbPath, roots);
builder.Services.AddSingleton<IDbService>(dbService);
```

---

## Frontend — campo `Root` en resultados

`SearchResult` ahora incluye `root`. El frontend muestra el nombre del root debajo del título del resultado, similar al path actual. Cambios en `types.ts` y `SearchView.tsx`.

---

## Tests

### Existentes — ajustes menores
- `When_LogEndpointIsRequested` — mockear `IDbService` además de `ILogService` si el endpoint lo requiere.
- `When_WatcherServiceDetectsChange` — ajustar constructor para `IReadOnlyList<string> roots`.

### Nuevo — `When_DbServiceSearches`
Crea una DB en temp, indexa un archivo markdown de prueba, verifica:
- Búsqueda de frase exacta devuelve resultado
- `IsPathAllowed` con path dentro del root → `true`
- `IsPathAllowed` con path fuera del root → `false`
- `IndexDirectories` con root inexistente no explota

---

## Archivos a crear/modificar

| Archivo | Acción |
|---|---|
| `src/Api/AppConfig.cs` | Eliminar |
| `src/Api/Persistence/IDbService.cs` | Crear |
| `src/Api/Persistence/DbService.cs` | Refactorizar (instancia, SQL const, transacciones) |
| `src/Api/Services/WatcherService.cs` | Múltiples roots |
| `src/Api/Endpoints/SearchEndpoints.cs` | Usar `IDbService`, `IsPathAllowed` |
| `src/Api/Models/SearchResult.cs` | Agregar campo `Root` |
| `src/Api/Models/ErrorResult.cs` | Crear |
| `src/Api/Program.cs` | `IConfiguration`, DI, middleware |
| `app/src/types.ts` | Agregar `root` a `SearchResult` |
| `app/src/components/SearchView.tsx` | Mostrar `root` en resultados |
| `test/Api.Tests/When_DbServiceSearches.cs` | Crear |
| `test/Api.Tests/When_WatcherServiceDetectsChange.cs` | Ajustar constructor |
