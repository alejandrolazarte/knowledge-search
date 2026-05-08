# Features Roadmap

Ideas de nuevas funcionalidades para knowledge-search, ordenadas por prioridad de implementación.

---

## 1. ✅ Resaltado de términos en resultados
**Estado:** pendiente  
**Complejidad:** baja

SQLite FTS5 incluye la función `highlight()` que devuelve el texto del resultado con los términos buscados envueltos en marcadores HTML (`<mark>`). El backend retorna ese HTML en el JSON y el frontend lo renderiza.

**Cambios necesarios:**
- `DbService.cs`: usar `highlight(fts, col, '<mark>', '</mark>')` en la query de búsqueda
- `Models.cs`: agregar campo `HighlightedContent` al record `SearchResult`
- `SearchView.tsx`: renderizar el campo con `dangerouslySetInnerHTML` + estilos para `<mark>`

---

## 2. FileSystemWatcher — Re-indexado automático
**Estado:** pendiente  
**Complejidad:** baja-media

.NET tiene `FileSystemWatcher` built-in. El backend observa el directorio `knowledge/` y dispara re-indexado incremental al detectar cambios (crear, modificar, eliminar archivos `.md`). El frontend puede recibir el estado vía polling ligero o SSE.

**Cambios necesarios:**
- `Program.cs`: registrar `FileSystemWatcher` como hosted service
- `DbService.cs`: exponer método de indexación incremental (ya existe, solo conectarlo)
- Opcional: endpoint SSE `GET /events` para notificar al frontend en tiempo real

---

## 3. Operadores de búsqueda booleanos
**Estado:** pendiente  
**Complejidad:** baja

FTS5 soporta nativamente `AND`, `OR`, `NOT`, `NEAR(term1 term2, distancia)`. Solo falta exponerlo en la UI con una pequeña guía de sintaxis.

**Cambios necesarios:**
- `DbService.cs`: pasar la query sin escapado cuando el usuario usa operadores explícitos
- `SearchView.tsx`: tooltip o chips con ejemplos de sintaxis (`golang AND error`, `deploy NOT docker`)
- Validación básica para evitar queries malformadas

---

## 4. Navegación con teclado en resultados
**Estado:** pendiente  
**Complejidad:** baja (solo frontend)

Flechas `↑` `↓` para moverse entre resultados de búsqueda, `Enter` para abrir el archivo en el modal. Complementa el `Ctrl+K` ya existente para un flujo 100% teclado.

**Cambios necesarios:**
- `SearchView.tsx`: `useEffect` con listener de `keydown`, estado de índice activo
- Estilos: resaltar resultado activo con ring/border
- Sin cambios en backend

---

## 5. Búsqueda unificada (knowledge + skills)
**Estado:** pendiente  
**Complejidad:** media

Un solo campo de búsqueda que consulta ambas fuentes simultáneamente y agrupa los resultados por sección (Knowledge / Skills). Actualmente son dos vistas completamente separadas.

**Cambios necesarios:**
- `SearchEndpoints.cs`: nuevo endpoint `GET /search/all` que combina resultados
- `App.tsx`: nueva vista unificada o modo toggle en la búsqueda actual
- `types.ts`: discriminar tipo de resultado (`source: 'knowledge' | 'skill'`)

---

## 6. Explorador de archivos (árbol)
**Estado:** pendiente  
**Complejidad:** media

Tercera vista que muestra `knowledge/` como árbol de directorios navegable. Clic en un archivo lo abre en el `FileModal` existente. Útil para explorar cuando no sabes qué buscar.

**Cambios necesarios:**
- `SearchEndpoints.cs`: nuevo endpoint `GET /tree` que retorna JSON con estructura de árbol
- Nuevo componente `TreeView.tsx`: árbol colapsable con íconos de archivo/carpeta
- `Sidebar.tsx`: agregar ítem de navegación a la nueva vista

---

## 7. Estadísticas del índice
**Estado:** pendiente  
**Complejidad:** baja

Endpoint que retorna métricas del índice: número de archivos, secciones totales, tamaño de la BD, última vez que se indexó. Se muestra en la sidebar como info contextual.

**Cambios necesarios:**
- `SearchEndpoints.cs`: nuevo endpoint `GET /stats`
- `DbService.cs`: queries agregadas sobre la tabla FTS5
- `Sidebar.tsx`: sección colapsable con las estadísticas

---

## 8. Búsqueda semántica con embeddings locales
**Estado:** pendiente  
**Complejidad:** alta

Integrar Ollama (corriendo local) para generar embeddings vectoriales de los documentos. Búsqueda por similitud semántica como complemento al BM25: encuentra conceptos relacionados aunque no uses las mismas palabras.

**Cambios necesarios:**
- Nueva dependencia: cliente HTTP para Ollama API
- `DbService.cs`: tabla adicional para vectores (o usar sqlite-vec)
- Pipeline de indexación paralelo al FTS5 existente
- UI: toggle "búsqueda semántica / exacta"
