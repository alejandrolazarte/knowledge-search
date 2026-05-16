# Sources UI — Design Spec

**Fecha:** 2026-05-16  
**Estado:** Aprobado

---

## Objetivo

Agregar una pantalla `Sources` para administrar las carpetas que alimentan Knowledge Search,
Repo Search y Code Graph.

La primera versión debe ser funcional: leer la configuración actual, editarla desde la UI y guardar
cambios reales en `data/sources.json`.

Quedan fuera de esta primera entrega:

- selector nativo de carpetas.
- reindex por source.
- separación del proceso indexer.
- montaje dinámico de rutas en Podman.

---

## Layout

La pantalla usa el layout elegido en el mockup B:

- lista compacta de sources a la izquierda.
- panel de detalle editable a la derecha.

Este layout mantiene la UI densa y práctica para uso diario: se puede escanear una lista de
repositorios y bases de conocimiento, seleccionar una source, editar reglas y guardar sin pasar por
un wizard.

El botón `Add folder` crea una source nueva en la lista y selecciona el panel de detalle. En esta
primera versión el path se escribe manualmente.

---

## Navegación

El sidebar agrega una entrada `Sources`.

El header muestra `Sources` cuando la vista está activa. No se agrega una página de onboarding ni
explicación extensa dentro de la app.

---

## Modelo editable

La UI edita estas propiedades:

- `id`
- `name`
- `kind`: `knowledge` o `repository`
- `hostPath`
- `indexCode`
- `indexDocs`
- `docIncludes`
- `codeIncludes`
- `excludes`

Defaults al crear una source:

### `repository`

- `indexCode: true`
- `indexDocs: true`
- `docIncludes`: `README.md`, `docs/**/*.md`, `specs/**/*.md`, `adr/**/*.md`
- `codeIncludes`: extensiones soportadas por el code graph actual.
- `excludes`: `.git`, `node_modules`, `bin`, `obj`, `dist`, `build`, `.next`, `coverage`

### `knowledge`

- `indexCode: false`
- `indexDocs: true`
- `docIncludes`: `**/*.md`, `**/*.mdx`
- `codeIncludes`: defaults presentes pero desactivados por `indexCode: false`
- `excludes`: mismos defaults.

---

## Backend

Agregar endpoints:

### `GET /sources`

Devuelve la configuración editable con defaults aplicados.

Si `data/sources.json` no existe, devuelve una configuración generada desde `KNOWLEDGE_DIRS` o
`KnowledgeDirs`, igual que `ResolveSources()`.

### `PUT /sources`

Valida y guarda `data/sources.json`.

Validaciones mínimas:

- `sources` no puede ser `null`.
- cada source debe tener `id`, `name`, `kind` y `hostPath`.
- `id` debe ser único.
- `kind` debe ser `knowledge` o `repository`.
- no se exige que `hostPath` exista en disco en esta primera versión, porque puede depender de
  rutas host/container o montajes todavía no configurados.

Al guardar:

- crear `data/` si no existe.
- escribir JSON indentado.
- preservar defaults explícitos para que el archivo exportado sea fácil de leer.

### `GET /sources/export`

Devuelve el JSON actual para descargar o copiar.

Si el archivo no existe, devuelve la configuración generada en memoria desde el fallback.

### Import JSON

La primera versión puede implementar import del lado UI usando `PUT /sources`:

1. el usuario elige o pega un JSON.
2. la UI parsea el contenido.
3. la UI llama `PUT /sources`.

No hace falta un endpoint separado `POST /sources/import`.

---

## Runtime Después De Guardar

Después de `PUT /sources`, la API debe usar la configuración actualizada para endpoints que leen
sources, especialmente `/roots`.

No hace falta que el `DbService` singleton cambie sus roots en caliente en esta primera entrega.
Para evitar prometer algo que todavía no existe:

- `/sources` y `/roots` reflejan el JSON guardado.
- el indexado existente sigue usando los roots cargados al iniciar la API hasta que se implemente la
  capa de runtime/indexer.

La UI debe mostrar un mensaje corto después de guardar: `Saved. Restart or reindex flow required for active index roots.`

---

## Frontend

Crear `SourcesView`.

### Lista izquierda

Cada fila muestra:

- `kind` como chip.
- `name`.
- `hostPath`.
- chips `code` y `docs`.
- estado básico: `saved`, `edited`, `error`.

### Panel derecho

Campos:

- `Name`
- `ID`
- `Kind`
- `Host path`
- toggle `Index code`
- toggle `Index docs`
- textarea `Doc includes`
- textarea `Code includes`
- textarea `Excludes`

Acciones:

- `Save changes`
- `Delete`
- `Add folder`
- `Import JSON`
- `Export JSON`

Los textareas editan una regla por línea.

---

## Errores

La UI debe manejar:

- error al cargar `/sources`.
- JSON inválido al importar.
- validación fallida al guardar.
- error del servidor al guardar.

Los errores aparecen como texto compacto en la pantalla, no como modal.

---

## Testing

Backend:

- `GET /sources` devuelve fallback si no existe `sources.json`.
- `PUT /sources` guarda JSON válido.
- `PUT /sources` rechaza ids duplicados.
- `GET /sources/export` devuelve JSON generado si no existe archivo.
- `/roots` refleja sources con `indexDocs: true` después de guardar.

Frontend:

- `SourcesView` renderiza sources cargadas.
- editar una source habilita estado dirty.
- guardar llama `PUT /sources`.
- importar JSON inválido muestra error.
- exportar descarga o copia el JSON actual.

---

## Decisiones

- La primera versión guarda cambios reales en `data/sources.json`.
- No se implementa selector nativo de carpetas todavía.
- No se implementa reindex por source todavía.
- No se promete recargar `DbService` en caliente; eso queda para la separación de indexer/runtime.
- El layout principal es lista izquierda + detalle derecho.
