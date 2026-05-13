# knowledge-search

Motor de búsqueda full-text para uno o más directorios de conocimiento y visor de skills. Indexa archivos Markdown en SQLite con FTS5 (trigramas + BM25). Incluye una UI en React + Tailwind con tema dark/light.

## Requisitos

- .NET 10 SDK
- Node.js 18+ (solo para desarrollo del frontend)

## Uso

### Levantar la API + UI

**Con hot reload (recomendado para desarrollo):**

```bash
cd knowledge-search/src/Api
dotnet watch run
```

El servidor recarga automáticamente al guardar cambios `.cs`. Workflow recomendado: **terminá todos los cambios antes de guardar** para evitar recargas con código a mitad de refactor.

**Sin watch (one-shot):**

```bash
cd knowledge-search/src/Api
dotnet run
```

Abre `http://localhost:5111` en el navegador.

### Vistas

- **Knowledge Search**: búsqueda FTS con BM25, filtros por root/modo, copia path con un clic, botón Re-index
- **Skills**: grid con todas las skills de `~/.claude/skills`, filtro por nombre/descripción, panel lateral con markdown renderizado

## Documentación

- GitHub hardening: documentado en `D:\Documentation\Projects\knowledge-search\github-hardening.md`.

### Variables de entorno

| Variable | Default |
|---|---|
| `KNOWLEDGE_DB` | `../knowledge.db` (relativo al `Api/`) |
| `KnowledgeDirs` / `KNOWLEDGE_DIRS` | `../knowledge` (relativo al `Api/`), múltiples roots separados por `;` |
| `KnowledgeDir` / `KNOWLEDGE_DIR` | compatibilidad con un único root |
| `SKILLS_DIR` | `~/.claude/skills` |

### API REST

| Endpoint | Descripción |
|---|---|
| `GET /search?q=texto&limit=5&modes=phrase,and,or&roots=knowledge` | Busca en el índice (BM25) |
| `POST /index` | Re-indexa el directorio (incremental) |
| `GET /roots` | Lista roots configurados |
| `GET /file?path=...` | Lee un archivo permitido dentro de un root |
| `GET /image?path=...` | Sirve imágenes locales referenciadas por Markdown |
| `GET /skills` | Lista de skills (nombre, descripción) |
| `GET /skills/{dir}` | Contenido crudo de una skill |
| `GET /health` | Health check |

## Desarrollo del frontend

```bash
cd knowledge-search/app
npm install
npm run dev   # dev server en :5173, proxy a :5111
```

Para generar el build de producción (emite a `Api/`):

```bash
npm run build
```

**Stack:** Vite 6 · React 19 · TypeScript 5 · Tailwind 3 · react-markdown
