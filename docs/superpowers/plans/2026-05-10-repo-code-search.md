# Plan: Repo Code Search con BM25

## Summary

Agregar una busqueda separada para codigo escaneado desde repositorios, usando SQLite FTS5/BM25 como el Knowledge Search actual, pero alimentada por el Code Graph.

La feature permite buscar contexto real de clases, interfaces, records, enums y metodos escaneados, rankeado por relevancia, con filtros por repo/kind y los mismos modos phrase, and, or.

## Key Changes

- Extender CodeGraphRepository con un indice FTS5 code_docs separado de docs.
- Guardar documentos de codigo durante /repos/scan junto con code_nodes y code_edges.
- Exponer GET /repos/code-search?q=...&limit=10&modes=phrase,and,or&repos=a,b&kinds=Class,Method.
- Exponer GET /repos/file?path=... para leer archivos que pertenecen a repos escaneados.
- Agregar vista Repo Search separada del Knowledge Search y del Code Graph.

## Indexacion

- V1 indexa contenido por nodo detectado.
- Para cada CodeNode se lee el archivo y se extrae contenido desde la linea del simbolo hasta antes del siguiente simbolo del mismo archivo.
- Si no hay siguiente simbolo, se toma una ventana limitada de lineas posteriores.
- Se guardan repo, identifier, name, kind, file_path, line y content.
- Se respetan los directorios excluidos por el scan: node_modules, .git, bin y obj.

## Tests

- Buscar por nombre de simbolo.
- Buscar por contenido dentro del bloque indexado.
- Filtrar por repo, kind y repo + kind.
- Validar modos phrase, and y or.
- Validar que re-escanear un repo reemplaza documentos y no duplica resultados.
- Validar errores 400 para query vacia, modes invalido y kinds invalido.
- Validar UI: vista Repo Search, filtros, resultados, expand/collapse, copiar path y empty state.

## Assumptions

- El indice de codigo usa la misma base SQLite que CodeGraph.
- La busqueda de codigo queda separada del search de documentacion.
- No se agregan embeddings ni busqueda semantica en esta iteracion.
- No se cambia el comportamiento actual de Code Graph; solo se aprovecha su scan para alimentar el nuevo indice.
