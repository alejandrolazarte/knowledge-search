import { useEffect, useState } from 'react'
import { MarkdownContent } from './MarkdownContent'
import { usePanelResize } from '../hooks/usePanelResize'
import type { Skill } from '../types'

interface Props {
  skill:   Skill | null
  onClose: () => void
}

function splitFrontmatter(text: string): { fm: string | null; body: string } {
  if (!text.startsWith('---')) return { fm: null, body: text }
  const end = text.indexOf('\n---', 3)
  if (end === -1) return { fm: null, body: text }
  return {
    fm:   text.slice(0, end + 4),
    body: text.slice(end + 4).trimStart(),
  }
}

export function SkillPanel({ skill, onClose }: Props) {
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [error,   setError]   = useState(false)
  const { width, onMouseDown } = usePanelResize()

  useEffect(() => {
    if (!skill) return
    setLoading(true)
    setContent('')
    setError(false)
    fetch(`/skills/${encodeURIComponent(skill.dirName)}`)
      .then(r => r.text())
      .then(t => setContent(t))
      .catch(() => setError(true))
      .finally(() => setLoading(false))
  }, [skill])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  const open = !!skill

  return (
    <>
      {open && (
        <div className="fixed inset-0 bg-black/40 z-10" onClick={onClose} />
      )}
      <div
        style={{ width: open ? width : 0 }}
        className="fixed top-0 right-0 h-full bg-gh-surface border-l border-gh-border z-20 flex flex-col overflow-hidden transition-[width] duration-150"
      >
        <div
          onMouseDown={onMouseDown}
          className="absolute top-0 left-0 w-1 h-full cursor-ew-resize hover:bg-gh-accent/40"
        />
        <div className="flex items-center justify-between px-4 py-2.5 border-b border-gh-border shrink-0">
          <span className="text-sm font-medium text-gh-text truncate pr-4">{skill?.name}</span>
          <button onClick={onClose} className="text-gh-muted hover:text-gh-text shrink-0">
            <svg width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div className="flex-1 overflow-y-auto p-4">
          {loading && <p className="text-gh-muted text-sm">Cargando…</p>}
          {error   && <p className="text-red-400 text-sm">Error al cargar</p>}
          {!loading && !error && content && (() => {
            const { fm, body } = splitFrontmatter(content)
            return (
              <>
                {fm && (
                  <pre className="text-xs text-gh-muted font-mono bg-gh-card border border-gh-border rounded p-2 mb-3 whitespace-pre-wrap">
                    {fm}
                  </pre>
                )}
                <MarkdownContent className="prose prose-invert prose-sm max-w-none
                  prose-headings:text-gh-text prose-p:text-gh-muted
                  prose-pre:bg-transparent prose-pre:p-0 prose-pre:my-1
                  prose-a:text-gh-accent prose-strong:text-gh-text prose-li:text-gh-muted
                  prose-table:text-xs prose-th:text-gh-text prose-td:text-gh-muted">
                  {body}
                </MarkdownContent>
              </>
            )
          })()}
        </div>
      </div>
    </>
  )
}
