import { useState, useRef } from 'react'
import { MarkdownContent } from './MarkdownContent'
import { FileModal } from './FileModal'
import type { SearchResult } from '../types'

// ── History ───────────────────────────────────────────────────────────────────
const HISTORY_KEY = 'ks-search-history'
const MAX_HISTORY = 8

function loadHistory(): string[] {
  try { return JSON.parse(localStorage.getItem(HISTORY_KEY) ?? '[]') } catch { return [] }
}
function pushHistory(q: string, prev: string[]): string[] {
  const next = [q, ...prev.filter(h => h !== q)].slice(0, MAX_HISTORY)
  localStorage.setItem(HISTORY_KEY, JSON.stringify(next))
  return next
}

// ── Types ─────────────────────────────────────────────────────────────────────
const LIMITS = [5, 10, 20] as const
type Limit = typeof LIMITS[number]

interface Props {
  statusMsg: string
  onStatus:  (msg: string) => void
  inputRef?: React.RefObject<HTMLInputElement | null>
}

// ── Component ─────────────────────────────────────────────────────────────────
export function SearchView({ statusMsg, onStatus, inputRef: externalRef }: Props) {
  const localRef                          = useRef<HTMLInputElement>(null)
  const inputRef                          = externalRef ?? localRef
  const [query,         setQuery]         = useState('')
  const [searchedQuery, setSearchedQuery] = useState('')
  const [results,       setResults]       = useState<SearchResult[]>([])
  const [loading,       setLoading]       = useState(false)
  const [expanded,      setExpanded]      = useState<Set<number>>(new Set())
  const [history,       setHistory]       = useState<string[]>(loadHistory)
  const [showHistory,   setShowHistory]   = useState(false)
  const [limit,         setLimit]         = useState<Limit>(10)
  const [copiedIdx,     setCopiedIdx]     = useState<number | null>(null)
  const [openFile,      setOpenFile]      = useState<string | null>(null)

  // ── Search ──────────────────────────────────────────────────────────────────
  const doSearch = async (q = query) => {
    const trimmed = q.trim()
    if (!trimmed) return
    setLoading(true)
    setExpanded(new Set())
    setShowHistory(false)
    onStatus('Buscando…')
    try {
      const data: SearchResult[] = await fetch(
        `/search?q=${encodeURIComponent(trimmed)}&limit=${limit}`
      ).then(r => r.json())
      setResults(data)
      setSearchedQuery(trimmed)
      setHistory(prev => pushHistory(trimmed, prev))
      onStatus(data.length
        ? `${data.length} resultado${data.length !== 1 ? 's' : ''} para "${trimmed}"`
        : `Sin resultados para "${trimmed}"`)
    } catch (e: unknown) {
      onStatus('Error: ' + (e instanceof Error ? e.message : 'desconocido'))
    } finally {
      setLoading(false)
    }
  }

  const clearSearch = () => {
    setQuery('')
    setResults([])
    setSearchedQuery('')
    setShowHistory(false)
    onStatus('')
    inputRef.current?.focus()
  }

  // ── Keyboard ────────────────────────────────────────────────────────────────
  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter')  { doSearch(); return }
    if (e.key === 'Escape') {
      if (query) clearSearch()
      else { setShowHistory(false); inputRef.current?.blur() }
    }
  }

  // ── Expand / collapse ───────────────────────────────────────────────────────
  const toggleExpanded = (i: number) =>
    setExpanded(prev => { const s = new Set(prev); s.has(i) ? s.delete(i) : s.add(i); return s })

  const expandAll  = () => setExpanded(new Set(results.map((_, i) => i)))
  const collapseAll = () => setExpanded(new Set())
  const allExpanded = results.length > 0 && results.every((_, i) => expanded.has(i))

  // ── Copy helpers ─────────────────────────────────────────────────────────────
  const copyPath = (r: SearchResult) => {
    const text = `${r.path.replace(/\\/g, '/').split('/').slice(-3).join('/')}:${r.line}`
    navigator.clipboard.writeText(text).then(() => onStatus(`Copiado: ${text}`))
  }

  const copyContent = (r: SearchResult, i: number) => {
    navigator.clipboard.writeText(r.content).then(() => {
      setCopiedIdx(i)
      setTimeout(() => setCopiedIdx(null), 1500)
    })
  }

  const hasSearched = searchedQuery !== ''

  // ── Render ──────────────────────────────────────────────────────────────────
  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 gap-3">

      {/* ── Search bar ── */}
      <div className="flex gap-2 items-center">
        <div className="relative flex-1">
          <input
            ref={inputRef}
            value={query}
            onChange={e => { setQuery(e.target.value); setShowHistory(true) }}
            onFocus={() => setShowHistory(true)}
            onBlur={() => setTimeout(() => setShowHistory(false), 150)}
            onKeyDown={onKeyDown}
            placeholder="Buscar en knowledge/  ·  Ctrl+K"
            autoFocus
            className="w-full bg-gh-surface border border-gh-border rounded px-3 py-1.5 text-sm text-gh-text
              outline-none focus:border-gh-accent placeholder-gh-muted pr-7"
          />
          {query && (
            <button onClick={clearSearch} tabIndex={-1}
              className="absolute right-2 top-1/2 -translate-y-1/2 text-gh-muted hover:text-gh-text"
            >
              <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          )}

          {/* History dropdown */}
          {showHistory && history.length > 0 && !loading && (
            <div className="absolute top-full left-0 right-0 mt-1 bg-gh-surface border border-gh-border rounded-lg shadow-xl z-10 overflow-hidden">
              <div className="flex items-center justify-between px-3 py-1 border-b border-gh-border">
                <span className="text-[10px] text-gh-muted uppercase tracking-widest">Recientes</span>
                <button
                  onMouseDown={() => { localStorage.removeItem(HISTORY_KEY); setHistory([]) }}
                  className="text-[10px] text-gh-muted hover:text-gh-accent"
                >borrar</button>
              </div>
              {history.slice(0, 6).map((h, i) => (
                <button key={i} onMouseDown={() => { setQuery(h); doSearch(h) }}
                  className="flex items-center gap-2 w-full px-3 py-1.5 text-sm text-gh-muted hover:bg-gh-card hover:text-gh-text text-left"
                >
                  <svg width="11" height="11" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2} className="shrink-0 opacity-50">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z"/>
                  </svg>
                  <span className="truncate">{h}</span>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Limit selector */}
        <div className="flex gap-0.5 shrink-0">
          {LIMITS.map(l => (
            <button key={l} onClick={() => setLimit(l)}
              title={`Máximo ${l} resultados`}
              className={`px-2 py-1.5 text-xs rounded font-medium transition-colors border
                ${limit === l
                  ? 'bg-gh-accent text-white border-transparent'
                  : 'bg-gh-surface text-gh-muted border-gh-border hover:text-gh-text hover:bg-gh-card'}`}
            >{l}</button>
          ))}
        </div>

        <button onClick={() => doSearch()} disabled={loading}
          className="bg-gh-accent hover:opacity-90 disabled:opacity-50 text-white text-sm px-4 py-1.5 rounded font-medium shrink-0"
        >
          Buscar
        </button>
      </div>

      {/* ── Status + expand controls ── */}
      <div className="flex items-center justify-between min-h-[16px]">
        {statusMsg && !loading && (
          <p className="text-xs text-gh-muted">{statusMsg}</p>
        )}
        {!loading && results.length > 1 && (
          <button onClick={allExpanded ? collapseAll : expandAll}
            className="ml-auto text-xs text-gh-muted hover:text-gh-accent"
          >
            {allExpanded ? '▲ colapsar todo' : '▼ expandir todo'}
          </button>
        )}
      </div>

      {/* ── Results list ── */}
      <div className="flex-1 overflow-y-auto space-y-2 pr-1">

        {/* Skeleton loading */}
        {loading && [...Array(3)].map((_, i) => (
          <div key={i} className="bg-gh-surface border border-gh-border rounded-lg p-3 animate-pulse">
            <div className="flex items-center justify-between mb-2">
              <div className="h-3.5 bg-gh-card rounded w-40" />
              <div className="h-3 bg-gh-card rounded w-16" />
            </div>
            <div className="h-3 bg-gh-card rounded w-56 mb-3" />
            <div className="space-y-1.5">
              <div className="h-2.5 bg-gh-card rounded w-full" />
              <div className="h-2.5 bg-gh-card rounded w-5/6" />
              <div className="h-2.5 bg-gh-card rounded w-4/6" />
            </div>
          </div>
        ))}

        {/* Initial empty state */}
        {!loading && !hasSearched && (
          <div className="flex flex-col items-center justify-center h-52 gap-3 text-center select-none">
            <svg className="w-10 h-10 text-gh-border" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 15.803 7.5 7.5 0 0015.803 15.803z"/>
            </svg>
            <div>
              <p className="text-gh-muted text-sm">Buscá en el knowledge base</p>
              <p className="text-gh-border text-xs mt-1">
                <kbd className="bg-gh-card border border-gh-border rounded px-1 py-0.5 font-mono text-[10px]">Ctrl+K</kbd>
                {' '}para enfocar  ·
                <kbd className="bg-gh-card border border-gh-border rounded px-1 py-0.5 font-mono text-[10px] ml-1">Enter</kbd>
                {' '}para buscar
              </p>
            </div>
          </div>
        )}

        {/* No results after search */}
        {!loading && hasSearched && results.length === 0 && (
          <div className="flex flex-col items-center justify-center h-52 gap-3 text-center select-none">
            <svg className="w-10 h-10 text-gh-border" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15.182 16.318A4.486 4.486 0 0012.016 15a4.486 4.486 0 00-3.198 1.318M21 12a9 9 0 11-18 0 9 9 0 0118 0zM9.75 9.75c0 .414-.168.75-.375.75S9 10.164 9 9.75 9.168 9 9.375 9s.375.336.375.75zm-.375 0h.008v.015h-.008V9.75zm5.625 0c0 .414-.168.75-.375.75s-.375-.336-.375-.75.168-.75.375-.75.375.336.375.75zm-.375 0h.008v.015h-.008V9.75z"/>
            </svg>
            <div>
              <p className="text-gh-muted text-sm">Sin resultados para "<span className="text-gh-text">{searchedQuery}</span>"</p>
              <p className="text-gh-border text-xs mt-1">Probá con otros términos</p>
            </div>
          </div>
        )}

        {/* Result cards */}
        {!loading && results.map((r, i) => {
          const isExp  = expanded.has(i)
          const relPath = r.path.replace(/\\/g, '/').split('/').slice(-3).join('/')
          return (
            <div key={i}
              className="bg-gh-surface border border-gh-border rounded-lg p-3 hover:bg-gh-card transition-colors"
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm font-medium text-gh-text truncate pr-2">{r.title}</span>
                <div className="flex items-center gap-1 shrink-0">
                  {/* Ver archivo */}
                  <button onClick={() => setOpenFile(r.path)} title="Ver archivo completo"
                    className="p-1 rounded text-gh-muted hover:text-gh-accent hover:bg-gh-surface transition-colors"
                  >
                    <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M2.036 12.322a1.012 1.012 0 010-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178z"/>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/>
                    </svg>
                  </button>
                  {/* Copy content */}
                  <button onClick={() => copyContent(r, i)} title="Copiar contenido"
                    className="p-1 rounded text-gh-muted hover:text-gh-accent hover:bg-gh-surface transition-colors"
                  >
                    {copiedIdx === i
                      ? <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5} className="text-green-400">
                          <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5"/>
                        </svg>
                      : <svg width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                          <path strokeLinecap="round" strokeLinejoin="round" d="M15.666 3.888A2.25 2.25 0 0013.5 2.25h-3c-1.03 0-1.9.693-2.166 1.638m7.332 0c.055.194.084.4.084.612v0a.75.75 0 01-.75.75H9a.75.75 0 01-.75-.75v0c0-.212.03-.418.084-.612m7.332 0c.646.049 1.288.11 1.927.184 1.1.128 1.907 1.077 1.907 2.185V19.5a2.25 2.25 0 01-2.25 2.25H6.75A2.25 2.25 0 014.5 19.5V6.257c0-1.108.806-2.057 1.907-2.185a48.208 48.208 0 011.927-.184"/>
                        </svg>
                    }
                  </button>
                  <span className="text-[10px] bg-gh-card text-gh-muted border border-gh-border rounded px-1.5 py-0.5">
                    {r.section}
                  </span>
                </div>
              </div>

              <button onClick={() => copyPath(r)}
                className="text-xs text-gh-accent font-mono mb-2 hover:underline text-left block truncate w-full"
                title="Click para copiar ruta"
              >
                {relPath}:{r.line}
              </button>

              <div
                className={`overflow-hidden transition-all ${isExp ? '' : 'max-h-28'}`}
                style={{ maskImage: isExp ? 'none' : 'linear-gradient(to bottom, black 60%, transparent 100%)' }}
              >
                <MarkdownContent highlight={searchedQuery} className="prose prose-xs prose-invert max-w-none
                  prose-p:my-0.5 prose-p:text-xs prose-p:text-gh-muted
                  prose-headings:text-gh-text prose-headings:font-semibold prose-headings:my-1
                  prose-h1:text-sm prose-h2:text-sm prose-h3:text-xs
                  prose-pre:bg-transparent prose-pre:p-0 prose-pre:my-1
                  prose-a:text-gh-accent prose-a:no-underline hover:prose-a:underline
                  prose-strong:text-gh-text prose-strong:font-semibold
                  prose-ul:my-0.5 prose-li:my-0 prose-li:text-xs prose-li:text-gh-muted
                  prose-ol:my-0.5
                  prose-table:text-xs prose-th:text-gh-text prose-td:text-gh-muted prose-th:py-0.5 prose-td:py-0.5
                  prose-blockquote:border-gh-border prose-blockquote:text-gh-muted prose-blockquote:text-xs">
                  {r.content}
                </MarkdownContent>
              </div>

              <button onClick={() => toggleExpanded(i)}
                className="mt-1 text-[10px] text-gh-muted hover:text-gh-accent"
              >
                {isExp ? '▲ menos' : '▼ más'}
              </button>
            </div>
          )
        })}
      </div>

      <FileModal path={openFile} onClose={() => setOpenFile(null)} />
    </div>
  )
}
