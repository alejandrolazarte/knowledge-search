import { useState, useEffect, useCallback } from 'react'
import type { CrossRepoSubgraphResponse, CrossRepoSearchNode } from '../types'
import { GraphCanvas } from './GraphCanvas'

type DisplayMode = 'list' | 'graph'

const DEPTHS = [0, 1, 2, 3] as const
type Depth = typeof DEPTHS[number]

const KIND_COLORS: Record<string, string> = {
  Class: 'text-blue-400 border-blue-800/50 bg-blue-900/20',
  Interface: 'text-green-400 border-green-800/50 bg-green-900/20',
  Record: 'text-purple-400 border-purple-800/50 bg-purple-900/20',
  Enum: 'text-amber-400 border-amber-800/50 bg-amber-900/20',
  Method: 'text-gray-400 border-gray-700/50 bg-gray-800/20',
}

export function GraphView() {
  const [query,       setQuery]       = useState('')
  const [depth,       setDepth]       = useState<Depth>(2)
  const [result,      setResult]      = useState<CrossRepoSubgraphResponse | null>(null)
  const [loading,     setLoading]     = useState(false)
  const [statusMsg,   setStatusMsg]   = useState('')
  const [mode,        setMode]        = useState<DisplayMode>('list')
  const [scanPath,    setScanPath]    = useState('')
  const [actionMsg,   setActionMsg]   = useState('')
  const [actionBusy,  setActionBusy]  = useState(false)
  const [repos,       setRepos]       = useState<string[]>([])

  const fetchRepos = useCallback(async () => {
    try { setRepos(await fetch('/repos').then(r => r.json())) } catch {}
  }, [])

  useEffect(() => { fetchRepos() }, [fetchRepos])

  const doSearch = async (overrideQuery?: string) => {
    const q = (overrideQuery ?? query).trim()
    if (!q) return
    setLoading(true)
    setStatusMsg('Buscando…')
    setResult(null)
    try {
      const data: CrossRepoSubgraphResponse = await fetch(
        `/repos/search?q=${encodeURIComponent(q)}&depth=${depth}`
      ).then(r => r.json())
      setResult(data)
      const nodeCount = data.nodes.length
      const crossCount = data.crossRepoLinks.length
      setStatusMsg(
        nodeCount === 0
          ? `Sin resultados para "${q}"`
          : `${nodeCount} nodos · ${crossCount} conexión${crossCount !== 1 ? 'es' : ''} cross-repo`
      )
    } catch {
      setStatusMsg('Error al buscar')
    } finally {
      setLoading(false)
    }
  }

  const scanRepo = async () => {
    const path = scanPath.trim()
    if (!path) return
    setActionBusy(true)
    setActionMsg('Escaneando…')
    try {
      const data = await fetch('/repos/scan', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ directoryPath: path }),
      }).then(r => r.json())
      if (data.error) {
        setActionMsg(`✗ ${data.error}`)
      } else {
        setActionMsg(`✓ ${data.repositoryName}: ${data.filesScanned} archivos · ${data.nodesFound} nodos`)
        setScanPath('')
        fetchRepos()
      }
    } catch {
      setActionMsg('✗ Error al escanear')
    } finally {
      setActionBusy(false)
    }
  }

  const buildCrossRefs = async () => {
    setActionBusy(true)
    setActionMsg('Construyendo conexiones cross-repo…')
    try {
      const data = await fetch('/repos/cross-ref', { method: 'POST' }).then(r => r.json())
      setActionMsg(`✓ ${data.crossRepoEdgesFound} conexiones cross-repo encontradas`)
    } catch {
      setActionMsg('✗ Error al construir conexiones')
    } finally {
      setActionBusy(false)
    }
  }

  const nodesByRepo = result?.nodes.reduce<Record<string, CrossRepoSearchNode[]>>((acc, n) => {
    ;(acc[n.repositoryName] ??= []).push(n)
    return acc
  }, {}) ?? {}

  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 gap-3">

      {/* ── Search bar ── */}
      <div className="flex gap-2 items-center">
        <input
          value={query}
          onChange={e => setQuery(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && doSearch()}
          placeholder="Buscar en repos…  ej: IntegrationEvent, UserService"
          autoFocus
          className="flex-1 bg-gh-surface border border-gh-border rounded px-3 py-1.5 text-sm
            text-gh-text outline-none focus:border-gh-accent placeholder-gh-muted"
        />
        <div className="flex gap-0.5 shrink-0">
          {DEPTHS.map(d => (
            <button key={d} onClick={() => setDepth(d)} title={`BFS profundidad ${d}`}
              className={`px-2 py-1.5 text-xs rounded font-medium transition-colors border
                ${depth === d
                  ? 'bg-gh-accent text-white border-transparent'
                  : 'bg-gh-surface text-gh-muted border-gh-border hover:text-gh-text hover:bg-gh-card'}`}
            >{d}</button>
          ))}
        </div>
        <button onClick={() => doSearch()} disabled={loading || !query.trim()}
          className="bg-gh-accent hover:opacity-90 disabled:opacity-40 text-white text-sm
            px-4 py-1.5 rounded font-medium shrink-0"
        >
          Buscar
        </button>
      </div>

      {/* ── Scan toolbar ── */}
      <div className="flex items-center gap-2 flex-wrap">
        <input
          value={scanPath}
          onChange={e => setScanPath(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && scanRepo()}
          placeholder="Ruta del repo a escanear…"
          className="flex-1 min-w-48 bg-gh-surface border border-gh-border rounded px-3 py-1 text-xs
            text-gh-text outline-none focus:border-gh-accent placeholder-gh-muted"
        />
        <button onClick={scanRepo} disabled={actionBusy || !scanPath.trim()}
          className="text-xs border border-gh-border rounded px-2.5 py-1 text-gh-muted
            hover:text-gh-text hover:bg-gh-surface disabled:opacity-40 shrink-0 transition-colors"
        >
          Escanear
        </button>
        <button onClick={buildCrossRefs} disabled={actionBusy || repos.length < 2}
          title={repos.length < 2 ? 'Necesitás al menos 2 repos escaneados' : undefined}
          className="text-xs border border-gh-border rounded px-2.5 py-1 text-gh-muted
            hover:text-gh-text hover:bg-gh-surface disabled:opacity-40 shrink-0 transition-colors"
        >
          Build cross-refs
        </button>

        {/* Display mode toggle */}
        <div className="flex gap-0.5 ml-auto shrink-0">
          {(['list', 'graph'] as DisplayMode[]).map(m => (
            <button key={m} onClick={() => setMode(m)}
              className={`text-xs px-2.5 py-1 rounded border transition-colors
                ${mode === m
                  ? 'bg-gh-accent/20 text-gh-accent border-gh-accent/40'
                  : 'bg-gh-surface text-gh-muted border-gh-border hover:text-gh-text'}`}
            >{m === 'list' ? 'Lista' : 'Grafo'}</button>
          ))}
        </div>
      </div>

      {/* ── Status bar ── */}
      <div className="flex items-center gap-3 min-h-[20px]">
        {statusMsg && (
          <p className="text-xs text-gh-muted">{statusMsg}</p>
        )}
        {actionMsg && (
          <p className={`text-xs ${actionMsg.startsWith('✓') ? 'text-green-400' : actionMsg.startsWith('✗') ? 'text-red-400' : 'text-gh-muted'}`}>
            {actionMsg}
          </p>
        )}
        {repos.length > 0 && (
          <div className="flex gap-1 flex-wrap ml-auto">
            {repos.map(r => (
              <button key={r} onClick={() => { setQuery(r); doSearch(r) }}
                title={`Buscar todo en ${r}`}
                className="text-[10px] font-mono bg-gh-card border border-gh-border rounded
                  px-1.5 py-0.5 text-gh-muted hover:text-gh-accent hover:border-gh-accent/40 transition-colors"
              >
                {r}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* ── Content area ── */}
      <div className="flex-1 overflow-hidden">

        {loading && <LoadingSkeleton />}

        {!loading && !result && (
          <EmptyState />
        )}

        {!loading && result && mode === 'list' && (
          <div className="h-full overflow-y-auto space-y-4 pr-1">
            {Object.entries(nodesByRepo).map(([repoName, nodes]) => (
              <RepoGroup key={repoName} repoName={repoName} nodes={nodes} result={result} />
            ))}

            {result.crossRepoLinks.length > 0 && (
              <div className="bg-gh-surface border border-amber-800/40 rounded-lg p-3">
                <p className="text-xs font-semibold text-amber-400 mb-2">
                  Conexiones cross-repo ({result.crossRepoLinks.length})
                </p>
                <div className="space-y-1.5">
                  {result.crossRepoLinks.map((link, i) => (
                    <div key={i} className="flex items-center gap-2 text-xs font-mono">
                      <span className="text-gh-muted truncate max-w-[200px]">
                        <span className="text-amber-400/70">{link.sourceRepositoryName}</span>
                        {' :: '}
                        <span className="text-gh-text">{link.sourceIdentifier.split('.').pop()}</span>
                      </span>
                      <svg className="w-4 h-3 shrink-0 text-amber-500" fill="none" viewBox="0 0 16 12" stroke="currentColor" strokeWidth={1.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M1 6h12M9 2l4 4-4 4" strokeDasharray="3,1.5" />
                      </svg>
                      <span className="text-gh-muted truncate max-w-[200px]">
                        <span className="text-amber-400/70">{link.targetRepositoryName}</span>
                        {' :: '}
                        <span className="text-gh-text">{link.targetIdentifier.split('.').pop()}</span>
                      </span>
                      <span className="text-[10px] text-amber-600 ml-auto shrink-0">{link.kind}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {result.nodes.length === 0 && (
              <div className="flex flex-col items-center justify-center h-40 gap-2 text-center">
                <p className="text-gh-muted text-sm">Sin resultados para "<span className="text-gh-text">{result.query}</span>"</p>
                <p className="text-gh-border text-xs">Probá con otro término o reducí la profundidad</p>
              </div>
            )}
          </div>
        )}

        {!loading && result && mode === 'graph' && (
          <GraphCanvas result={result} />
        )}
      </div>
    </div>
  )
}

function RepoGroup({ repoName, nodes, result }: {
  repoName: string
  nodes: CrossRepoSearchNode[]
  result: CrossRepoSubgraphResponse
}) {
  const repoEdges = result.edges.filter(e => e.repositoryName === repoName)

  return (
    <div className="bg-gh-surface border border-gh-border rounded-lg overflow-hidden">
      <div className="px-3 py-2 border-b border-gh-border bg-gh-card flex items-center gap-2">
        <span className="text-xs font-semibold text-gh-text font-mono">{repoName}</span>
        <span className="text-[10px] text-gh-muted">{nodes.length} nodo{nodes.length !== 1 ? 's' : ''}</span>
        {repoEdges.length > 0 && (
          <span className="text-[10px] text-gh-muted">{repoEdges.length} edge{repoEdges.length !== 1 ? 's' : ''}</span>
        )}
      </div>
      <div className="p-2 grid grid-cols-1 gap-1.5">
        {nodes.map(node => <NodeCard key={node.identifier} node={node} />)}
      </div>
      {repoEdges.length > 0 && (
        <div className="px-3 pb-2 space-y-1">
          <p className="text-[10px] text-gh-muted uppercase tracking-wider pt-1">Edges</p>
          {repoEdges.map((edge, i) => (
            <div key={i} className="flex items-center gap-1.5 text-[10px] font-mono text-gh-muted">
              <span className="text-gh-text truncate">{edge.sourceIdentifier.split('.').pop()}</span>
              <span className="text-gh-border">→</span>
              <span className="text-gh-text truncate">{edge.targetIdentifier.split('.').pop()}</span>
              <span className="ml-auto shrink-0 text-gh-border">{edge.kind}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function NodeCard({ node }: { node: CrossRepoSearchNode }) {
  const kindClass = KIND_COLORS[node.kind] ?? KIND_COLORS.Method
  const relPath = node.filePath.replace(/\\/g, '/').split('/').slice(-2).join('/')

  return (
    <div className="flex items-start gap-2 px-2 py-1.5 rounded hover:bg-gh-card transition-colors">
      <span className={`text-[10px] font-mono border rounded px-1 py-0.5 shrink-0 mt-0.5 ${kindClass}`}>
        {node.kind[0]}
      </span>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="text-sm text-gh-text font-medium truncate">{node.name}</span>
          {node.weight > 0 && (
            <span className="text-[10px] text-gh-muted shrink-0" title="Conexiones">⬡ {node.weight}</span>
          )}
        </div>
        <span className="text-[10px] text-gh-muted font-mono truncate block">{relPath}:{node.line}</span>
      </div>
    </div>
  )
}

function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center h-full gap-3 text-center select-none">
      <svg className="w-10 h-10 text-gh-border" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.2}>
        <path strokeLinecap="round" strokeLinejoin="round"
          d="M12 21a9 9 0 100-18 9 9 0 000 18zm0 0v-9m0 0l-3 3m3-3l3 3M12 3v3"/>
      </svg>
      <div>
        <p className="text-gh-muted text-sm">Buscá clases, eventos o servicios a través de tus repos</p>
        <p className="text-gh-border text-xs mt-1">Escaneá un repo con el campo de arriba, luego buscá</p>
      </div>
    </div>
  )
}

function LoadingSkeleton() {
  return (
    <div className="space-y-3 animate-pulse">
      {[1, 2].map(i => (
        <div key={i} className="bg-gh-surface border border-gh-border rounded-lg p-3">
          <div className="h-3 bg-gh-card rounded w-32 mb-3" />
          <div className="space-y-2">
            <div className="h-3 bg-gh-card rounded w-48" />
            <div className="h-3 bg-gh-card rounded w-40" />
          </div>
        </div>
      ))}
    </div>
  )
}
