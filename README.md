# knowledge-search

Motor de búsqueda full-text para el directorio `knowledge/` y visor de skills. Indexa archivos Markdown en SQLite con FTS5 (trigramas + BM25). Incluye una UI en React + Tailwind con tema dark/light.

## Requisitos

- .NET 10 SDK
- Node.js 18+ (solo para desarrollo del frontend)

## Uso

### Levantar la API + UI

```bash
cd knowledge-search/Api
dotnet run Program.cs
```

Abre `http://localhost:5111` en el navegador.

### Vistas

- **Knowledge Search**: búsqueda FTS con BM25, copia path con un clic, botón Re-index
- **Skills**: grid con todas las skills de `~/.claude/skills`, filtro por nombre/descripción, panel lateral con markdown renderizado

### Variables de entorno

| Variable | Default |
|---|---|
| `KNOWLEDGE_DB` | `../knowledge.db` (relativo al `Api/`) |
| `KNOWLEDGE_DIR` | directorio raíz del repo |
| `SKILLS_DIR` | `~/.claude/skills` |

### API REST

| Endpoint | Descripción |
|---|---|
| `GET /search?q=texto&limit=5` | Busca en el índice (BM25) |
| `POST /index` | Re-indexa el directorio (incremental) |
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
