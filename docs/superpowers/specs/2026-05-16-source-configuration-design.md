# Source Configuration + Indexer Split — Design Spec

**Fecha:** 2026-05-16  
**Estado:** Aprobado

---

## Objetivo

Reemplazar la configuración rígida de roots y el escaneo manual de repos por un modelo de
**sources** configurables desde la UI y desde un archivo JSON importable/exportable.

La intención es que levantar la app sea cómodo: agregar una carpeta, decidir si es una base de
conocimiento o un repositorio, ajustar includes/excludes cuando haga falta, y dejar que la app
indexe lo correcto sin pelear con rutas de Windows, Linux, macOS o Podman.

Este diseño también prepara el siguiente paso: separar el proceso de indexado/escaneo de la API,
dejando la API enfocada en consultas y el indexer como escritor principal de la base.

---

## Modelo mental

La unidad de configuración es una `source`.

Una source representa una carpeta del host que puede alimentar uno o más índices:

- **Knowledge Search**: Markdown y documentación.
- **Repo Search**: código indexado para búsqueda textual.
- **Code Graph**: símbolos y relaciones entre archivos/repositorios.

El usuario no debería tener que agregar la misma carpeta dos veces. Si un repositorio contiene
`README.md`, `docs/`, `specs/` o ADRs, esa documentación se considera parte del repositorio y puede
alimentar Knowledge Search.

---

## Tipos de source

### `knowledge`

Carpeta de documentación suelta o base de conocimiento.

Defaults:

- `indexDocs: true`
- `indexCode: false`
- incluye Markdown de forma amplia: `**/*.md`, `**/*.mdx`

### `repository`

Repositorio de código.

Defaults:

- `indexCode: true`
- `indexDocs: true`
- indexa código para Repo Search y Code Graph.
- indexa documentación interna para Knowledge Search.

No se expone un tipo `both` en la UI. La carpeta sigue siendo conceptualmente un repositorio, pero
con toggles para decidir si indexa código, documentación o ambos.

---

## Archivo de configuración

La configuración de usuario vive en JSON, no en `appsettings.json`.

Ubicación propuesta:

```text
data/sources.json
```

Ejemplo:

```json
{
  "version": 1,
  "sources": [
    {
      "id": "orders-ms",
      "name": "orders-ms",
      "kind": "repository",
      "hostPath": "D:\\work\\orders-ms",
      "indexCode": true,
      "indexDocs": true,
      "docIncludes": [
        "README.md",
        "docs/**/*.md",
        "specs/**/*.md",
        "adr/**/*.md"
      ],
      "codeIncludes": [
        "**/*.cs",
        "**/*.ts",
        "**/*.tsx",
        "**/*.js",
        "**/*.jsx",
        "**/*.py"
      ],
      "excludes": [
        "**/.git/**",
        "**/node_modules/**",
        "**/bin/**",
        "**/obj/**",
        "**/dist/**",
        "**/build/**",
        "**/.next/**",
        "**/coverage/**"
      ]
    }
  ]
}
```

`appsettings.json` queda para configuración técnica de la app. `sources.json` queda para
configuración editable por el usuario y por la UI.

---

## Includes y excludes

La app define defaults por tipo de source y permite override por source.

Excludes por defecto:

```json
[
  "**/.git/**",
  "**/node_modules/**",
  "**/bin/**",
  "**/obj/**",
  "**/dist/**",
  "**/build/**",
  "**/.next/**",
  "**/coverage/**"
]
```

Includes por defecto para documentación en repos:

```json
[
  "README.md",
  "docs/**/*.md",
  "specs/**/*.md",
  "adr/**/*.md"
]
```

Includes por defecto para bases de conocimiento:

```json
[
  "**/*.md",
  "**/*.mdx"
]
```

Los defaults aparecen en la pantalla de configuración para que se puedan ver y editar. La UI debe
permitir agregar excludes específicos sin obligar al usuario a editar JSON a mano.

---

## UI de configuración

Agregar una pantalla de configuración de sources.

Controles esperados:

- listar sources configuradas.
- agregar carpeta como `Base de conocimiento` o `Repositorio`.
- editar nombre, tipo, toggles `indexCode` e `indexDocs`.
- editar includes/excludes.
- importar `sources.json`.
- exportar `sources.json`.
- reindexar una source específica.
- mostrar estado: pendiente, indexando, actualizado, error.

Para repositorios nuevos, los defaults son código y documentación activados.

---

## Rutas host vs container

El JSON guarda rutas del host en `hostPath`.

Los scripts de Podman son responsables de montar esas rutas dentro del container con paths estables.
La app y el indexer trabajan con rutas de container, pero la UI muestra la ruta original del host.

Ejemplo conceptual:

```text
hostPath:       D:\work\orders-ms
containerPath:  /app/sources/orders-ms
```

Esto evita que el usuario tenga que conocer rutas internas de Podman. También mantiene el archivo
JSON portable entre máquinas, con la salvedad esperada de que cada host puede necesitar rutas
distintas.

Si una source no está montada o no existe, la UI debe mostrar un error claro y no intentar indexarla.

---

## API vs indexer

Este diseño no implementa todavía la separación de procesos, pero deja el contrato preparado.

Objetivo posterior:

- **API**: proceso de consulta. Lee índices, sirve archivos permitidos y reporta estado.
- **Indexer**: proceso de trabajo. Escanea sources, observa cambios, actualiza SQLite y escribe logs.

La API no debería ejecutar trabajos pesados como `/index`, `/repos/scan` o `/repos/cross-ref` en el
largo plazo. Esos comandos deberían convertirse en jobs del indexer.

La primera versión puede mantener una sola SQLite compartida con WAL y transacciones cortas:

- API como lectora principal.
- Indexer como escritor principal.

---

## Compatibilidad

Durante la migración, `KNOWLEDGE_DIRS` puede seguir existiendo como compatibilidad:

- si `data/sources.json` existe, se usa `sources.json`.
- si no existe, se genera una configuración inicial desde `KNOWLEDGE_DIRS`.

Esto evita romper el flujo actual mientras se incorpora la pantalla de configuración.

---

## Testing

Tests esperados cuando se implemente:

- carga válida de `sources.json`.
- generación inicial desde `KNOWLEDGE_DIRS`.
- defaults correctos para `knowledge`.
- defaults correctos para `repository`.
- excludes por defecto aplicados a `.git`, `node_modules`, `bin`, `obj`.
- repo con `indexDocs: true` alimenta Knowledge Search con `README.md`, `docs/`, `specs/` y `adr/`.
- repo con `indexCode: false` no alimenta Repo Search ni Code Graph.
- source inexistente reporta error sin romper la API.

---

## Decisiones

- Los repositorios indexan código y documentación por defecto.
- La distinción entre `knowledge` y `repository` se mantiene porque es útil para la UI, el agente y
  las búsquedas.
- No se agrega tipo visible `both`; se usan toggles.
- `sources.json` es la fuente de verdad para carpetas configuradas por el usuario.
- La resolución host/container pertenece a los scripts de Podman, no a la UI.
- La separación del indexer queda como paso posterior, no como parte obligatoria de la primera
  implementación de configuración.
