import { useEffect, useMemo, useState } from 'react'
import { MarkdownContent } from './MarkdownContent'
import type { CodeDocumentSearchResult } from '../types'

type ModeName = 'phrase' | 'and' | 'or'
interface ActiveModes { phrase: boolean; and: boolean; or: boolean }

const DEFAULT_MODES: ActiveModes = { phrase: true, and: true, or: true }
const MODES_KEY = 'repo-search-modes'
const REPOS_KEY = 'repo-search-repos'
const KINDS_KEY = 'repo-search-kinds'
const LIMITS = [5, 10, 20, 50] as const
const KINDS = ['Class', 'Interface', 'Record', 'Enum', 'Method'] as const

function loadJson<T>(key: string, fallback: T): T {
  try { return { ...fallback, ...JSON.parse(localStorage.getItem(key) ?? '{}') } } catch { return fallback }
}

function loadArray(key: string): string[] {
  try { return JSON.parse(localStorage.getItem(key) ?? '[]') } catch { return [] }
}

function loadOptionalArray(key: string): string[] | null {
  try {
    const value = localStorage.getItem(key)
    return value ? JSON.parse(value) : null
  } catch {
    return null
  }
}

function modesParam(modes: ActiveModes): string {
  const active = (['phrase', 'and', 'or'] as ModeName[]).filter(m => modes[m])
  return active.length === 3 ? '' : active.join(',')
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
    case 'py': return 'python'
    case 'json': return 'json'
    case 'xml':
    case 'html': return 'xml'
    case 'yml':
    case 'yaml': return 'yaml'
    default: return 'plaintext'
  }
}

function codeBlock(content: string, path: string): string {
  return `\`\`\`${languageForPath(path)}\n${content.trim()}\n\`\`\``
}

interface Props {
  onOpenFile: (path: string, endpoint?: string) => void
}

export function RepoSearchView({ onOpenFile }: Props) {
  const [query, setQuery] = useState('')
  const [searchedQuery, setSearchedQuery] = useState('')
  const [results, setResults] = useState<CodeDocumentSearchResult[]>([])
  const [loading, setLoading] = useState(false)
  const [statusMsg, setStatusMsg] = useState('')
  const [limit, setLimit] = useState<typeof LIMITS[number]>(10)
  const [repos, setRepos] = useState<string[]>([])
  const [selectedRepos, setSelectedRepos] = useState<string[]>([])
  const [selectedKinds, setSelectedKinds] = useState<string[]>(() => loadOptionalArray(KINDS_KEY) ?? [...KINDS])
  const [activeModes, setActiveModes] = useState<ActiveModes>(() => loadJson(MODES_KEY, DEFAULT_MODES))
  const [expanded, setExpanded] = useState<Set<number>>(new Set())
  const [copiedIdx, setCopiedIdx] = useState<number | null>(null)

  useEffect(() => {
    fetch('/repos')
      .then(r => r.json() as Promise<string[]>)
      .then(data => {
        setRepos(data)
        const stored = loadOptionalArray(REPOS_KEY)
        setSelectedRepos(stored ? stored.filter(repo => data.includes(repo)) : data)
      })
      .catch(() => setRepos([]))
  }, [])

  const toggleMode = (mode: ModeName) => {
    setActiveModes(prev => {
      const next = { ...prev, [mode]: !prev[mode] }
      if (!next.phrase && !next.and && !next.or) return prev
      localStorage.setItem(MODES_KEY, JSON.stringify(next))
      return next
    })
  }

  const saveSelectedRepos = (next: string[]) => {
    setSelectedRepos(next)
    localStorage.setItem(REPOS_KEY, JSON.stringify(next))
  }

  const saveSelectedKinds = (next: string[]) => {
    setSelectedKinds(next)
    localStorage.setItem(KINDS_KEY, JSON.stringify(next))
  }

  const doSearch = async () => {
    const q = query.trim()
    if (!q) return
    if (selectedRepos.length === 0 || selectedKinds.length === 0) {
      setResults([])
      setSearchedQuery(q)
      setStatusMsg('Seleccioná al menos un repo y un kind')
      return
    }
    setLoading(true)
    setExpanded(new Set())
    setStatusMsg('Buscando código…')
    try {
      const modesStr = modesParam(activeModes)
      const url = `/repos/code-search?q=${encodeURIComponent(q)}&limit=${limit}` +
        (modesStr ? `&modes=${modesStr}` : '') +
        (selectedRepos.length < repos.length ? `&repos=${selectedRepos.map(encodeURIComponent).join(',')}` : '') +
        (selectedKinds.length < KINDS.length ? `&kinds=${selectedKinds.join(',')}` : '')
      const data: CodeDocumentSearchResult[] = await fetch(url).then(r => r.json())
      setResults(data)
      setSearchedQuery(q)
      setStatusMsg(data.length
        ? `${data.length} resultado${data.length !== 1 ? 's' : ''} de código para "${q}"`
        : `Sin resultados de código para "${q}"`)
    } catch {
      setStatusMsg('Error al buscar código')
    } finally {
      setLoading(false)
    }
  }

  const copyPath = (result: CodeDocumentSearchResult, index: number) => {
    const relPath = `${result.filePath.replace(/\\/g, '/').split('/').slice(-4).join('/')}:${result.line}`
    navigator.clipboard.writeText(relPath).then(() => {
      setCopiedIdx(index)
      setTimeout(() => setCopiedIdx(null), 1500)
    })
  }

  const toggleExpanded = (index: number) =>
    setExpanded(prev => {
      const next = new Set(prev)
      next.has(index) ? next.delete(index) : next.add(index)
      return next
    })

  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 gap-3">
      <div className="flex gap-2 items-center">
        <input
          value={query}
          onChange={e => setQuery(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && doSearch()}
          placeholder="Buscar en código escaneado…  ej: ConfigureServices, IntegrationEvent"
          autoFocus
          className="flex-1 bg-gh-surface border border-gh-border rounded px-3 py-1.5 text-sm
            text-gh-text outline-none focus:border-gh-accent placeholder-gh-muted"
        />
        <div className="flex gap-0.5 shrink-0">
          {LIMITS.map(value => (
            <button key={value} onClick={() => setLimit(value)}
              className={`px-2 py-1.5 text-xs rounded font-medium border transition-colors
                ${limit === value
                  ? 'bg-gh-accent text-white border-transparent'
                  : 'bg-gh-surface text-gh-muted border-gh-border hover:text-gh-text hover:bg-gh-card'}`}
            >{value}</button>
          ))}
        </div>
        <button onClick={doSearch} disabled={loading || !query.trim()}
          className="bg-gh-accent hover:opacity-90 disabled:opacity-40 text-white text-sm
            px-4 py-1.5 rounded font-medium shrink-0"
        >
          Buscar
        </button>
      </div>

      <div className="flex items-center gap-2 flex-wrap">
        {(['phrase', 'and', 'or'] as ModeName[]).map(mode => (
          <button key={mode} onClick={() => toggleMode(mode)}
            className={`text-[10px] font-mono px-1.5 py-0.5 rounded border transition-colors
              ${activeModes[mode]
                ? 'bg-gh-accent/20 text-gh-accent border-gh-accent/40'
                : 'bg-gh-surface text-gh-muted border-gh-border opacity-50'}`}
          >
            {mode.toUpperCase()}
          </button>
        ))}
        <MultiSelectDropdown
          label="Repos"
          options={repos}
          selected={selectedRepos}
          onChange={saveSelectedRepos}
          searchable
        />
        <MultiSelectDropdown
          label="Kind"
          options={[...KINDS]}
          selected={selectedKinds}
          onChange={saveSelectedKinds}
        />
      </div>

      <div className="min-h-[18px]">
        {statusMsg && !loading && <p className="text-xs text-gh-muted">{statusMsg}</p>}
      </div>

      <div className="flex-1 overflow-y-auto space-y-2 pr-1">
        {loading && [...Array(3)].map((_, i) => (
          <div key={i} className="bg-gh-surface border border-gh-border rounded-lg p-3 animate-pulse">
            <div className="h-3.5 bg-gh-card rounded w-56 mb-2" />
            <div className="h-3 bg-gh-card rounded w-72 mb-3" />
            <div className="space-y-1.5">
              <div className="h-2.5 bg-gh-card rounded w-full" />
              <div className="h-2.5 bg-gh-card rounded w-5/6" />
            </div>
          </div>
        ))}

        {!loading && !searchedQuery && (
          <div className="flex flex-col items-center justify-center h-52 gap-2 text-center select-none">
            <p className="text-gh-muted text-sm">Buscá contexto dentro del código escaneado</p>
            <p className="text-gh-border text-xs">Escaneá repos desde Code Graph y volvé acá para buscar por contenido</p>
          </div>
        )}

        {!loading && searchedQuery && results.length === 0 && (
          <div className="flex flex-col items-center justify-center h-52 gap-2 text-center select-none">
            <p className="text-gh-muted text-sm">Sin resultados para "<span className="text-gh-text">{searchedQuery}</span>"</p>
            <p className="text-gh-border text-xs">Probá otro término o quitá filtros</p>
          </div>
        )}

        {!loading && results.map((result, index) => {
          const isExpanded = expanded.has(index)
          const relPath = result.filePath.replace(/\\/g, '/').split('/').slice(-4).join('/')
          return (
            <div key={`${result.repositoryName}:${result.identifier}:${index}`}
              className="bg-gh-surface border border-gh-border rounded-lg p-3 hover:bg-gh-card transition-colors"
            >
              <div className="flex items-start justify-between gap-3 mb-1">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 min-w-0">
                    <span className="text-sm font-medium text-gh-text truncate">{result.name}</span>
                    <span className="text-[10px] font-mono border border-purple-800/50 bg-purple-900/20 text-purple-300 rounded px-1 py-0.5 shrink-0">
                      {result.kind}
                    </span>
                    <span className="text-[10px] font-mono border border-blue-800/50 bg-blue-900/20 text-blue-400 rounded px-1 py-0.5 shrink-0">
                      {result.repositoryName}
                    </span>
                  </div>
                  <button onClick={() => copyPath(result, index)}
                    className="text-xs text-gh-accent font-mono hover:underline text-left truncate block max-w-full mt-1"
                    title="Click para copiar ruta"
                  >
                    {relPath}:{result.line}
                  </button>
                </div>
                <div className="flex gap-1 shrink-0">
                  <button onClick={() => onOpenFile(result.filePath, '/repos/file')}
                    className="text-xs border border-gh-border rounded px-2 py-0.5 text-gh-muted hover:text-gh-text hover:bg-gh-surface"
                  >
                    Ver
                  </button>
                  <button onClick={() => copyPath(result, index)}
                    className="text-xs border border-gh-border rounded px-2 py-0.5 text-gh-muted hover:text-gh-text hover:bg-gh-surface"
                  >
                    {copiedIdx === index ? 'copiado' : 'copiar'}
                  </button>
                </div>
              </div>

              <div className={`mt-2 overflow-hidden rounded border border-gh-border bg-gh-bg ${isExpanded ? '' : 'max-h-36'}`}>
                <MarkdownContent
                  className="prose prose-xs prose-invert max-w-none
                    prose-pre:m-0 prose-pre:rounded-none prose-pre:border-0
                    prose-code:text-xs prose-code:bg-transparent prose-code:p-0"
                  docPath={result.filePath}
                >
                  {codeBlock(result.content, result.filePath)}
                </MarkdownContent>
              </div>
              <button onClick={() => toggleExpanded(index)}
                className="mt-1 text-[10px] text-gh-muted hover:text-gh-accent"
              >
                {isExpanded ? '▲ menos' : '▼ más'}
              </button>
            </div>
          )
        })}
      </div>

    </div>
  )
}

function MultiSelectDropdown({
  label,
  options,
  selected,
  onChange,
  searchable = false,
}: {
  label: string
  options: string[]
  selected: string[]
  onChange: (selected: string[]) => void
  searchable?: boolean
}) {
  const [open, setOpen] = useState(false)
  const [filter, setFilter] = useState('')
  const selectedSet = useMemo(() => new Set(selected), [selected])
  const visibleOptions = options.filter(option =>
    option.toLowerCase().includes(filter.trim().toLowerCase())
  )
  const allSelected = selected.length === options.length && options.length > 0
  const summary = allSelected
    ? 'todos'
    : selected.length === 0
      ? 'ninguno'
      : `${selected.length}/${options.length}`

  const toggle = (option: string) => {
    onChange(selectedSet.has(option)
      ? selected.filter(item => item !== option)
      : [...selected, option])
  }

  return (
    <div className="relative">
      <button
        onClick={() => setOpen(prev => !prev)}
        className="flex items-center gap-1.5 text-[10px] font-mono px-2 py-1 rounded border border-gh-border
          bg-gh-surface text-gh-muted hover:text-gh-text hover:bg-gh-card transition-colors"
      >
        <span className="text-gh-border">{label}</span>
        <span className={selected.length === 0 ? 'text-red-400' : 'text-gh-accent'}>{summary}</span>
        <svg width="10" height="10" viewBox="0 0 20 20" fill="currentColor" className={open ? 'rotate-180' : ''}>
          <path fillRule="evenodd" d="M5.23 7.21a.75.75 0 011.06.02L10 11.17l3.71-3.94a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z" clipRule="evenodd" />
        </svg>
      </button>

      {open && (
        <div className="absolute left-0 top-full z-30 mt-1 w-72 rounded border border-gh-border bg-gh-surface shadow-xl">
          <div className="border-b border-gh-border p-2 space-y-2">
            {searchable && (
              <input
                value={filter}
                onChange={e => setFilter(e.target.value)}
                placeholder={`Buscar ${label.toLowerCase()}…`}
                className="w-full rounded border border-gh-border bg-gh-bg px-2 py-1 text-xs text-gh-text
                  outline-none focus:border-gh-accent placeholder-gh-muted"
                autoFocus
              />
            )}
            <div className="flex items-center gap-2">
              <button
                onClick={() => onChange(options)}
                className="text-[10px] text-gh-muted hover:text-gh-accent border border-gh-border rounded px-2 py-0.5"
              >
                todos
              </button>
              <button
                onClick={() => onChange([])}
                className="text-[10px] text-gh-muted hover:text-red-400 border border-gh-border rounded px-2 py-0.5"
              >
                ninguno
              </button>
              <span className="ml-auto text-[10px] text-gh-border">{selected.length} seleccionados</span>
            </div>
          </div>

          <div className="max-h-64 overflow-y-auto p-1">
            {visibleOptions.length === 0 && (
              <div className="px-2 py-3 text-xs text-gh-muted text-center">Sin coincidencias</div>
            )}
            {visibleOptions.map(option => (
              <label
                key={option}
                className="flex items-center gap-2 rounded px-2 py-1.5 text-xs text-gh-muted hover:bg-gh-card hover:text-gh-text"
              >
                <input
                  type="checkbox"
                  checked={selectedSet.has(option)}
                  onChange={() => toggle(option)}
                  className="h-3 w-3 accent-gh-accent"
                />
                <span className="truncate font-mono">{option}</span>
              </label>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
