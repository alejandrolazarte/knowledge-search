import { useEffect, useRef, useState, useCallback } from 'react'
import type { CrossRepoSubgraphResponse } from '../types'

const REPO_PALETTE = ['#58a6ff', '#56d364', '#e3b341', '#f78166', '#d2a8ff', '#79c0ff', '#ffa657', '#ff7b72']
const KIND_ABBREV: Record<string, string> = { Class: 'C', Interface: 'I', Record: 'R', Enum: 'E', Method: 'M' }
const NODE_R = 20
const MAX_NODES = 500
const MAX_ITER = 500
const MAX_SIM_EDGES = 1400
const MAX_VELOCITY = 18
const MAX_FORCE = 3.5

function repoColor(name: string): string {
  let h = 0
  for (let i = 0; i < name.length; i++) { h = ((h << 5) - h) + name.charCodeAt(i); h |= 0 }
  return REPO_PALETTE[Math.abs(h) % REPO_PALETTE.length]
}

interface NodeSim { id: string; label: string; kind: string; repo: string; weight: number; x: number; y: number; vx: number; vy: number }
interface EdgeSim { id: string; srcId: string; tgtId: string; kind: string; cross: boolean }

interface Props { result: CrossRepoSubgraphResponse }

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, value))
}

function finiteOr(value: number, fallback: number) {
  return Number.isFinite(value) ? value : fallback
}

export function GraphCanvas({ result }: Props) {
  const svgRef = useRef<SVGSVGElement>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const nodesRef = useRef<NodeSim[]>([])
  const edgesRef = useRef<EdgeSim[]>([])
  const nodeElements = useRef<Map<string, SVGGElement>>(new Map())
  const lineElements = useRef<Map<string, SVGLineElement>>(new Map())
  const rafRef = useRef(0)
  const iterRef = useRef(0)
  const frameRef = useRef(0)

  const [renderNodes, setRenderNodes] = useState<NodeSim[]>([])
  const [renderEdges, setRenderEdges] = useState<EdgeSim[]>([])
  const [viewBox, setViewBox] = useState({ x: -50, y: -50, w: 900, h: 700 })
  const [panOrigin, setPanOrigin] = useState<{ x: number; y: number } | null>(null)
  const [selected, setSelected] = useState<string | null>(null)
  const [hovered, setHovered] = useState<string | null>(null)

  useEffect(() => {
    if (result.nodes.length === 0 || result.nodes.length > MAX_NODES) return

    nodeElements.current.clear()
    lineElements.current.clear()

    const w = svgRef.current?.clientWidth ?? 800
    const h = svgRef.current?.clientHeight ?? 600
    const cx = w / 2, cy = h / 2
    const repos = [...new Set(result.nodes.map(n => n.repositoryName))]
    const repoIndex = new Map(repos.map((repo, i) => [repo, i]))
    const repoCounts = result.nodes.reduce<Map<string, number>>((acc, n) => {
      acc.set(n.repositoryName, (acc.get(n.repositoryName) ?? 0) + 1)
      return acc
    }, new Map())
    const repoSeen = new Map<string, number>()
    const repoRadius = Math.max(340, Math.min(w, h) * 0.58)
    setViewBox({
      x: Math.round(cx - w * 1.05),
      y: Math.round(cy - h * 0.95),
      w: Math.round(w * 2.1),
      h: Math.round(h * 1.9),
    })

    nodesRef.current = result.nodes.map((n, i) => ({
      id: `${n.repositoryName}::${n.identifier}`,
      label: n.name,
      kind: n.kind,
      repo: n.repositoryName,
      weight: n.weight,
      x: 0,
      y: 0,
      vx: 0, vy: 0,
    })).map((node, i) => {
      const ri = repoIndex.get(node.repo) ?? 0
      const repoAngle = repos.length === 1 ? 0 : (2 * Math.PI * ri) / repos.length
      const repoCx = cx + (repos.length === 1 ? 0 : repoRadius * Math.cos(repoAngle))
      const repoCy = cy + (repos.length === 1 ? 0 : repoRadius * Math.sin(repoAngle))
      const localIndex = repoSeen.get(node.repo) ?? 0
      repoSeen.set(node.repo, localIndex + 1)
      const localCount = repoCounts.get(node.repo) ?? 1
      const localAngle = (2 * Math.PI * localIndex) / Math.max(1, Math.ceil(Math.sqrt(localCount)) * 3)
      const localRadius = 55 + 34 * Math.sqrt(localIndex)
      return {
        ...node,
        x: Math.round(repoCx + localRadius * Math.cos(localAngle) + (i % 7) * 2),
        y: Math.round(repoCy + localRadius * Math.sin(localAngle) + (i % 5) * 2),
      }
    })

    let edgeCounter = 0
    const intra: EdgeSim[] = result.edges.map(e => ({
      id: `e${edgeCounter++}`,
      srcId: `${e.repositoryName}::${e.sourceIdentifier}`,
      tgtId: `${e.repositoryName}::${e.targetIdentifier}`,
      kind: e.kind, cross: false,
    }))
    const cross: EdgeSim[] = result.crossRepoLinks.map(l => ({
      id: `e${edgeCounter++}`,
      srcId: `${l.sourceRepositoryName}::${l.sourceIdentifier}`,
      tgtId: `${l.targetRepositoryName}::${l.targetIdentifier}`,
      kind: l.kind, cross: true,
    }))
    const nodeIds = new Set(nodesRef.current.map(n => n.id))
    edgesRef.current = [...intra, ...cross].filter(e => nodeIds.has(e.srcId) && nodeIds.has(e.tgtId))
    const nodeIndex = new Map<string, number>()
    for (let i = 0; i < nodesRef.current.length; i++) nodeIndex.set(nodesRef.current[i].id, i)
    const simEdges = edgesRef.current
      .slice()
      .sort((a, b) => Number(a.cross) - Number(b.cross))
      .slice(0, MAX_SIM_EDGES)

    iterRef.current = 0
    frameRef.current = 0
    setRenderNodes([...nodesRef.current])
    setRenderEdges([...edgesRef.current])
    setSelected(null)

    cancelAnimationFrame(rafRef.current)

    const tick = () => {
      if (iterRef.current >= MAX_ITER) return
      const ns = nodesRef.current
      const n = ns.length
      const scale = Math.max(1, n / 80)
      const REPULSION = 11000 / Math.sqrt(scale)
      const IDEAL = 215 + 120 / Math.sqrt(scale)
      const SPRING_K = 0.007
      const DAMP = 0.56
      const GRAVITY = 0.003
      const bounds = Math.max(w, h, 800) * 6

      const fx = new Float64Array(n)
      const fy = new Float64Array(n)

      for (let i = 0; i < n; i++) {
        const a = ns[i]
        for (let j = i + 1; j < n; j++) {
          const b = ns[j]
          const dx = b.x - a.x, dy = b.y - a.y
          const d = Math.max(12, Math.sqrt(dx * dx + dy * dy) || 12)
          const f = Math.min(MAX_FORCE, REPULSION / (d * d))
          const nx = dx / d * f, ny = dy / d * f
          fx[i] -= nx; fy[i] -= ny; fx[j] += nx; fy[j] += ny
        }
      }

      for (const e of simEdges) {
        const si = nodeIndex.get(e.srcId), ti = nodeIndex.get(e.tgtId)
        if (si === undefined || ti === undefined) continue
        const dx = ns[ti].x - ns[si].x, dy = ns[ti].y - ns[si].y
        const d = Math.max(12, Math.sqrt(dx * dx + dy * dy) || 12)
        const f = clamp(SPRING_K * (d - IDEAL) * (e.cross ? 0.35 : 1), -MAX_FORCE, MAX_FORCE)
        const nx = dx / d * f, ny = dy / d * f
        fx[si] += nx; fy[si] += ny; fx[ti] -= nx; fy[ti] -= ny
      }

      for (let i = 0; i < n; i++) {
        const repo = repoIndex.get(ns[i].repo) ?? 0
        const repoAngle = repos.length === 1 ? 0 : (2 * Math.PI * repo) / repos.length
        const anchorX = cx + (repos.length === 1 ? 0 : repoRadius * Math.cos(repoAngle))
        const anchorY = cy + (repos.length === 1 ? 0 : repoRadius * Math.sin(repoAngle))
        fx[i] += (anchorX - ns[i].x) * GRAVITY
        fy[i] += (anchorY - ns[i].y) * GRAVITY
        ns[i].vx = clamp(finiteOr((ns[i].vx + fx[i]) * DAMP, 0), -MAX_VELOCITY, MAX_VELOCITY)
        ns[i].vy = clamp(finiteOr((ns[i].vy + fy[i]) * DAMP, 0), -MAX_VELOCITY, MAX_VELOCITY)
        ns[i].x = clamp(finiteOr(Math.round(ns[i].x + ns[i].vx), cx), -bounds, bounds)
        ns[i].y = clamp(finiteOr(Math.round(ns[i].y + ns[i].vy), cy), -bounds, bounds)
      }

      iterRef.current++
      frameRef.current++

      if (frameRef.current % 2 === 0) {
        const nodeEls = nodeElements.current
        for (let i = 0; i < n; i++) {
          const el = nodeEls.get(ns[i].id)
          if (el) el.setAttribute('transform', `translate(${ns[i].x},${ns[i].y})`)
        }

        const lineEls = lineElements.current
        const nodeMap = new Map(ns.map(n => [n.id, n]))
        for (const e of edgesRef.current) {
          const el = lineEls.get(e.id)
          if (!el) continue
          const src = nodeMap.get(e.srcId), tgt = nodeMap.get(e.tgtId)
          if (!src || !tgt) continue
          const dx = tgt.x - src.x, dy = tgt.y - src.y
          const d = Math.sqrt(dx * dx + dy * dy) || 1
          const r = NODE_R + 3
          el.setAttribute('x1', String(Math.round(src.x + dx / d * r)))
          el.setAttribute('y1', String(Math.round(src.y + dy / d * r)))
          el.setAttribute('x2', String(Math.round(tgt.x - dx / d * (r + 8))))
          el.setAttribute('y2', String(Math.round(tgt.y - dy / d * (r + 8))))
        }
      }

      rafRef.current = requestAnimationFrame(tick)
    }

    rafRef.current = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(rafRef.current)
  }, [result])

  const setNodeRef = useCallback((node: SVGGElement | null, id: string, x: number, y: number) => {
    if (node) {
      nodeElements.current.set(id, node)
      node.setAttribute('transform', `translate(${Math.round(x)},${Math.round(y)})`)
    }
  }, [])

  const setLineRef = useCallback((line: SVGLineElement | null, id: string, x1: number, y1: number, x2: number, y2: number) => {
    if (line) {
      lineElements.current.set(id, line)
      line.setAttribute('x1', String(Math.round(x1)))
      line.setAttribute('y1', String(Math.round(y1)))
      line.setAttribute('x2', String(Math.round(x2)))
      line.setAttribute('y2', String(Math.round(y2)))
    }
  }, [])

  const onMouseDown = (e: React.MouseEvent<SVGSVGElement>) => {
    if ((e.target as Element).closest('.graph-node')) return
    setPanOrigin({ x: e.clientX, y: e.clientY })
  }
  const onMouseMove = (e: React.MouseEvent<SVGSVGElement>) => {
    if (!panOrigin) return
    const scale = viewBox.w / (svgRef.current?.clientWidth ?? 800)
    const dx = (e.clientX - panOrigin.x) * scale
    const dy = (e.clientY - panOrigin.y) * scale
    setViewBox(v => ({ ...v, x: v.x - dx, y: v.y - dy }))
    setPanOrigin({ x: e.clientX, y: e.clientY })
  }
  const onMouseUp = () => setPanOrigin(null)

  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    const onWheelNative = (e: WheelEvent) => {
      e.preventDefault()
      e.stopPropagation()
      const f = e.deltaY > 0 ? 1.12 : 0.89
      setViewBox(v => ({ ...v, w: v.w * f, h: v.h * f }))
    }
    el.addEventListener('wheel', onWheelNative, { passive: false })
    return () => el.removeEventListener('wheel', onWheelNative)
  }, [])

  if (result.nodes.length > MAX_NODES) {
    return (
      <div className="flex items-center justify-center h-full text-gh-muted text-sm">
        Demasiados nodos ({result.nodes.length}) — refiná la búsqueda o usá la vista Lista.
      </div>
    )
  }

  const crossCount = renderEdges.filter(e => e.cross).length
  const selectedEdges = selected
    ? new Set(renderEdges.filter(e => e.srcId === selected || e.tgtId === selected).map(e => e.id))
    : null

  const visibleEdges = selected
    ? renderEdges.filter(e => !e.cross || selectedEdges!.has(e.id))
    : renderEdges.filter(e => !e.cross)

  const nodeMap = new Map(renderNodes.map(n => [n.id, n]))
  const highlightIds: Set<string> | null = selected
    ? new Set([selected, ...renderEdges.filter(e => e.srcId === selected || e.tgtId === selected).flatMap(e => [e.srcId, e.tgtId])])
    : null

  return (
    <div ref={containerRef} className="w-full h-full overflow-hidden">
    <svg ref={svgRef} className="w-full h-full select-none"
      style={{ cursor: panOrigin ? 'grabbing' : 'grab' }}
      viewBox={`${Math.round(viewBox.x)} ${Math.round(viewBox.y)} ${Math.round(viewBox.w)} ${Math.round(viewBox.h)}`}
      onMouseDown={onMouseDown} onMouseMove={onMouseMove}
      onMouseUp={onMouseUp} onMouseLeave={onMouseUp}
      onClick={() => setSelected(null)}
    >
      <defs>
        <marker id="arr-i" markerWidth="7" markerHeight="5" refX="7" refY="2.5" orient="auto">
          <polygon points="0 0,7 2.5,0 5" fill="#555" />
        </marker>
        <marker id="arr-x" markerWidth="7" markerHeight="5" refX="7" refY="2.5" orient="auto">
          <polygon points="0 0,7 2.5,0 5" fill="#e3b341" />
        </marker>
      </defs>

      {/* Capa de edges visibles */}
      {visibleEdges.map(edge => {
        const src = nodeMap.get(edge.srcId), tgt = nodeMap.get(edge.tgtId)
        if (!src || !tgt) return null
        const dx = tgt.x - src.x, dy = tgt.y - src.y
        const d = Math.sqrt(dx * dx + dy * dy) || 1
        const r = NODE_R + 3
        const x1 = Math.round(src.x + dx / d * r), y1 = Math.round(src.y + dy / d * r)
        const x2 = Math.round(tgt.x - dx / d * (r + 8)), y2 = Math.round(tgt.y - dy / d * (r + 8))
        const dim = highlightIds && !(highlightIds.has(edge.srcId) && highlightIds.has(edge.tgtId))
        return (
          <line key={edge.id} ref={(el) => setLineRef(el, edge.id, x1, y1, x2, y2)}
            stroke={edge.cross ? '#e3b341' : '#3d444d'}
            strokeWidth={edge.cross ? 2 : 1.5}
            strokeDasharray={edge.cross ? '7,3' : undefined}
            strokeOpacity={dim ? 0.1 : 0.85}
            markerEnd={`url(#arr-${edge.cross ? 'x' : 'i'})`}
          />
        )
      })}

      {/* Indicador de cross-repo edges ocultos */}
      {!selected && crossCount > 0 && (
        <text x="12" y="20" fontSize="10" fill="#e3b34188" fontFamily="sans-serif">
          +{crossCount} conexiones cross-repo — clic en un nodo para verlas
        </text>
      )}

      {/* Capa de nodos */}
      {renderNodes.map(node => {
        const color = repoColor(node.repo)
        const r = NODE_R + Math.min(node.weight, 6)
        const dim = highlightIds && !highlightIds.has(node.id)
        const isSel = selected === node.id
        const isHov = hovered === node.id
        return (
          <g key={node.id} ref={(el) => setNodeRef(el, node.id, node.x, node.y)}
            className="graph-node"
            opacity={dim ? 0.15 : 1} style={{ cursor: 'pointer' }}
            onClick={e => { e.stopPropagation(); setSelected(p => p === node.id ? null : node.id) }}
            onMouseEnter={() => setHovered(node.id)}
            onMouseLeave={() => setHovered(null)}
          >
            <circle r={r} fill={color + '18'} stroke={color}
              strokeWidth={isSel ? 3 : isHov ? 2 : 1.5}
              strokeDasharray={node.kind === 'Interface' ? '4,2' : undefined}
            />
            <text textAnchor="middle" dominantBaseline="middle"
              fontSize="10" fontWeight="700" fontFamily="monospace" fill={color}>
              {KIND_ABBREV[node.kind] ?? node.kind[0]}
            </text>
            <text y={r + 13} textAnchor="middle" fontSize="9" fill="#adb8c2" fontFamily="sans-serif">
              {node.label.length > 20 ? node.label.slice(0, 18) + '…' : node.label}
            </text>
            <text y={r + 23} textAnchor="middle" fontSize="8" fill={color + 'aa'} fontFamily="monospace">
              {node.repo}
            </text>
          </g>
        )
      })}
    </svg>
    </div>
  )
}
