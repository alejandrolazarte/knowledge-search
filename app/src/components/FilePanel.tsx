import { useEffect, useRef, useState } from 'react'
import { MarkdownContent } from './MarkdownContent'
import { usePanelResize } from '../hooks/usePanelResize'

interface Props {
  path:     string | null
  onClose:  () => void
  endpoint?: string
  onOpenFile?: (path: string, endpoint?: string) => void
}

function languageForPath(path: string): string {
  const extension = path.split('.').pop()?.toLowerCase()
  switch (extension) {
    case 'cs': return 'cs'
    case 'ts':
    case 'tsx': return 'ts'
    case 'js':
    case 'jsx':
    case 'mjs': return 'js'
    case 'py': return 'py'
    case 'json': return 'json'
    case 'xml':
    case 'html': return 'xml'
    case 'yml':
    case 'yaml': return 'yaml'
    default: return 'plaintext'
  }
}

function shouldRenderAsMarkdown(path: string): boolean {
  return /\.(md|mkd|markdown)$/i.test(path)
}

function renderableContent(path: string, content: string): string {
  if (shouldRenderAsMarkdown(path)) return content
  return `\`\`\`${languageForPath(path)}\n${content.trim()}\n\`\`\``
}

export function FilePanel({ path, onClose, endpoint = '/file', onOpenFile }: Props) {
  const [content,       setContent]       = useState('')
  const [originalContent, setOriginalContent] = useState('')
  const [loading,       setLoading]       = useState(false)
  const [editing,       setEditing]       = useState(false)
  const [saving,        setSaving]        = useState(false)
  const [saveError,     setSaveError]     = useState<string | null>(null)
  const [savedFlash,    setSavedFlash]    = useState(false)
  const [copied,        setCopied]        = useState(false)
  const textareaRef                       = useRef<HTMLTextAreaElement>(null)
  const { width, onMouseDown }            = usePanelResize()

  // Load file content
  useEffect(() => {
    if (!path) { setContent(''); setOriginalContent(''); setEditing(false); return }
    setLoading(true)
    setSaveError(null)
    fetch(`${endpoint}?path=${encodeURIComponent(path)}`)
      .then(r => r.text())
      .then(text => { setContent(text); setOriginalContent(text); setLoading(false) })
      .catch(() => { setContent('Error al cargar el archivo.'); setOriginalContent(''); setLoading(false) })
  }, [endpoint, path])

  const dirty   = editing && content !== originalContent
  const canEdit = !!path && shouldRenderAsMarkdown(path)

  const saveFile = async () => {
    if (!path || !dirty || saving) { return }
    setSaving(true)
    setSaveError(null)
    try {
      const response = await fetch(`${endpoint}?path=${encodeURIComponent(path)}`, {
        method:  'PUT',
        headers: { 'Content-Type': 'text/plain; charset=utf-8' },
        body:    content,
      })
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
      }
      setOriginalContent(content)
      setSavedFlash(true)
      setTimeout(() => setSavedFlash(false), 1500)
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : 'Error al guardar')
    } finally {
      setSaving(false)
    }
  }

  // Ctrl+S to save when editing
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 's') {
        if (path && editing) {
          e.preventDefault()
          saveFile()
        }
      }
      if (e.key === 'Escape' && path && !editing) {
        onClose()
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  })

  const handleClose = () => {
    if (dirty && !window.confirm('Hay cambios sin guardar. ¿Cerrar igual?')) { return }
    onClose()
  }

  const toggleEdit = () => {
    if (editing && dirty && !window.confirm('Hay cambios sin guardar. ¿Descartar?')) { return }
    if (editing) { setContent(originalContent); setSaveError(null) }
    setEditing(prev => !prev)
  }

  const copyPath = () => {
    if (!path) { return }
    const relPath = path.replace(/\\/g, '/').split('/').slice(-4).join('/')
    navigator.clipboard.writeText(relPath).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  if (!path) { return null }

  const relPath = path.replace(/\\/g, '/').split('/').slice(-4).join('/')

  return (
    <div
      style={{ width, flexShrink: 0 }}
      className="relative h-full bg-gh-surface border-l border-gh-border flex flex-col overflow-hidden"
    >
      <div
        onMouseDown={onMouseDown}
        className="absolute top-0 left-0 w-1 h-full cursor-ew-resize hover:bg-gh-accent/40 z-10"
      />
      <div className="flex items-center justify-between gap-3 px-4 py-2.5 border-b border-gh-border shrink-0">
        <span className="text-xs font-mono text-gh-accent truncate" title={relPath}>{relPath}</span>
        <div className="flex items-center gap-2 shrink-0">
          {canEdit && (
            <button
              onClick={toggleEdit}
              className={`text-xs border rounded px-2 py-0.5 transition-colors
                ${editing
                  ? 'border-gh-accent text-gh-accent'
                  : 'border-gh-border text-gh-muted hover:text-gh-text'}`}
              title={editing ? 'Salir del modo edición' : 'Editar archivo'}
            >
              {editing ? 'vista' : 'editar'}
            </button>
          )}
          {editing && (
            <button
              onClick={saveFile}
              disabled={!dirty || saving}
              className="text-xs border border-gh-border rounded px-2 py-0.5 text-gh-text hover:bg-gh-card disabled:opacity-40"
              title="Guardar (Ctrl+S)"
            >
              {saving ? 'guardando…' : savedFlash ? '✓ guardado' : 'guardar'}
            </button>
          )}
          <button
            onClick={copyPath}
            className="text-xs border border-gh-border rounded px-2 py-0.5 text-gh-muted hover:text-gh-text"
            title="Copiar ruta"
          >
            {copied ? '✓ copiado' : 'copiar ruta'}
          </button>
          <button
            onClick={handleClose}
            className="text-gh-muted hover:text-gh-text p-0.5"
            title="Cerrar (Esc)"
          >
            <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
      </div>

      {saveError && (
        <div className="px-4 py-1.5 bg-red-900/30 border-b border-red-700/40 text-xs text-red-300 shrink-0">
          {saveError}
        </div>
      )}

      <div className="flex-1 overflow-y-auto">
        {loading
          ? <div className="flex items-center justify-center h-full">
              <span className="text-sm text-gh-muted animate-pulse">Cargando…</span>
            </div>
          : editing
            ? <textarea
                ref={textareaRef}
                value={content}
                onChange={e => setContent(e.target.value)}
                spellCheck={false}
                className="w-full h-full bg-gh-bg text-gh-text font-mono text-xs p-4 outline-none resize-none border-0"
              />
            : <div className="px-6 py-4">
                <MarkdownContent
                  className="prose prose-sm prose-invert max-w-none
                    prose-p:text-gh-muted prose-p:my-1.5
                    prose-headings:text-gh-text prose-headings:font-semibold
                    prose-h1:text-lg prose-h1:mt-0 prose-h2:text-base prose-h3:text-sm
                    prose-pre:bg-gh-bg prose-pre:border prose-pre:border-gh-border prose-pre:rounded
                    prose-code:text-gh-accent prose-code:bg-gh-bg prose-code:px-1 prose-code:rounded prose-code:text-xs
                    prose-a:text-gh-accent prose-a:no-underline hover:prose-a:underline
                    prose-strong:text-gh-text
                    prose-ul:text-gh-muted prose-li:my-0.5 prose-li:text-sm
                    prose-ol:text-gh-muted
                    prose-table:text-sm prose-th:text-gh-text prose-td:text-gh-muted
                    prose-blockquote:border-gh-border prose-blockquote:text-gh-muted
                    prose-hr:border-gh-border"
                  docPath={path}
                  onOpenFile={onOpenFile}
                  fileEndpoint={endpoint}
                >
                  {renderableContent(path, content)}
                </MarkdownContent>
              </div>
        }
      </div>
    </div>
  )
}
