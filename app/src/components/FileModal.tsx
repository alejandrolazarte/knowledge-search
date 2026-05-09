import { useEffect, useState } from 'react'
import { MarkdownContent } from './MarkdownContent'

interface Props {
  path: string | null
  onClose: () => void
}

export function FileModal({ path, onClose }: Props) {
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [copied,  setCopied]  = useState(false)

  useEffect(() => {
    if (!path) { setContent(''); return }
    setLoading(true)
    fetch(`/file?path=${encodeURIComponent(path)}`)
      .then(r => r.text())
      .then(t => { setContent(t); setLoading(false) })
      .catch(() => { setContent('Error al cargar el archivo.'); setLoading(false) })
  }, [path])

  useEffect(() => {
    if (!path) return
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [path, onClose])

  if (!path) return null

  const relPath = path.replace(/\\/g, '/').split('/').slice(-4).join('/')

  const copyPath = () => {
    navigator.clipboard.writeText(relPath).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    })
  }

  return (
    /* Overlay */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center"
      style={{ background: 'rgba(0,0,0,0.65)' }}
      onClick={onClose}
    >
      {/* Modal */}
      <div
        className="relative flex flex-col bg-gh-surface border border-gh-border rounded-xl shadow-2xl"
        style={{ width: '72vw', maxWidth: 900, height: '80vh' }}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between gap-3 px-4 py-2.5 border-b border-gh-border shrink-0 bg-gh-bg rounded-t-xl">
          <span className="text-xs font-mono text-gh-accent truncate">{relPath}</span>
          <div className="flex items-center gap-2 shrink-0">
            <button
              onClick={copyPath}
              className="text-xs text-gh-muted hover:text-gh-text border border-gh-border rounded px-2 py-0.5 transition-colors"
            >
              {copied ? '✓ copiado' : 'copiar ruta'}
            </button>
            <button
              onClick={onClose}
              className="text-gh-muted hover:text-gh-text transition-colors p-0.5 rounded"
              title="Cerrar (Esc)"
            >
              <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto px-6 py-4">
          {loading
            ? <div className="flex items-center justify-center h-full">
                <span className="text-sm text-gh-muted animate-pulse">Cargando…</span>
              </div>
            : <MarkdownContent className="prose prose-sm prose-invert max-w-none
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
                docPath={path ?? undefined}>
                {content}
              </MarkdownContent>
          }
        </div>
      </div>
    </div>
  )
}
