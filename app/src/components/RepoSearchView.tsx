import { useEffect, useState } from 'react'
import { FileModal } from './FileModal'
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

export function RepoSearchView() {
  const [query, setQuery] = useState('')
  const [searchedQuery, setSearchedQuery] = useState('')
  const [results, setResults] = useState<CodeDocumentSearchResult[]>([])
  const [loading, setLoading] = useState(false)
  const [statusMsg, setStatusMsg] = useState('')
  const [limit, setLimit] = useState<typeof LIMITS[number]>(10)
  const [repos, setRepos] = useState<string[]>([])
  const [selectedRepos, setSelectedRepos] = useState<string[]>(() => loadArray(REPOS_KEY))
  const [selectedKinds, setSelectedKinds] = useState<string[]>(() => loadArray(KINDS_KEY))
  const [activeModes, setActiveModes] = useState<ActiveModes>(() => loadJson(MODES_KEY, DEFAULT_MODES))
  const [expanded, setExpanded] = useState<Set<number>>(new Set())
  const [openFile, setOpenFile] = useState<string | null>(null)
  const [copiedIdx, setCopiedIdx] = useState<number | null>(null)

  useEffect(() => {
    fetch('/repos')
      .then(r => r.json() as Promise<string[]>)
      .then(setRepos)
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

  const toggleRepo = (repo: string) => {
    setSelectedRepos(prev => {
      const next = prev.includes(repo) ? prev.filter(r => r !== repo) : [...prev, repo]
      localStorage.setItem(REPOS_KEY, JSON.stringify(next))
      return next
    })
  }

  const toggleKind = (kind: string) => {
    setSelectedKinds(prev => {
      const next = prev.includes(kind) ? prev.filter(k => k !== kind) : [...prev, kind]
      localStorage.setItem(KINDS_KEY, JSON.stringify(next))
      return next
    })
  }

  const doSearch = async () => {
    const q = query.trim()
    if (!q) return
    setLoading(true)
    setExpanded(new Set())
    setStatusMsg('Buscando código…')
    try {
      const modesStr = modesParam(activeModes)
      const url = `/repos/code-search?q=${encodeURIComponent(q)}&limit=${limit}` +
        (modesStr ? `&modes=${modesStr}` : '') +
        (selectedRepos.length ? `&repos=${selectedRepos.map(encodeURIComponent).join(',')}` : '') +
        (selectedKinds.length ? `&kinds=${selectedKinds.join(',')}` : '')
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
        <span className="text-[10px] text-gh-border">repos</span>
        {repos.map(repo => (
          <button key={repo} onClick={() => toggleRepo(repo)}
            className={`text-[10px] font-mono px-1.5 py-0.5 rounded border transition-colors
              ${selectedRepos.includes(repo)
                ? 'bg-blue-900/30 text-blue-400 border-blue-700/40'
                : 'bg-gh-surface text-gh-muted border-gh-border opacity-50'}`}
          >
            {repo}
          </button>
        ))}
        <span className="text-[10px] text-gh-border">kind</span>
        {KINDS.map(kind => (
          <button key={kind} onClick={() => toggleKind(kind)}
            className={`text-[10px] font-mono px-1.5 py-0.5 rounded border transition-colors
              ${selectedKinds.includes(kind)
                ? 'bg-purple-900/30 text-purple-300 border-purple-700/40'
                : 'bg-gh-surface text-gh-muted border-gh-border opacity-50'}`}
          >
            {kind}
          </button>
        ))}
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
                  <button onClick={() => setOpenFile(result.filePath)}
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

      <FileModal path={openFile} onClose={() => setOpenFile(null)} endpoint="/repos/file" />
    </div>
  )
}
