import { useEffect, useRef, useState } from 'react'
import type { CrossRepoSubgraphResponse } from '../types'

const REPO_PALETTE = ['#58a6ff', '#56d364', '#e3b341', '#f78166', '#d2a8ff', '#79c0ff', '#ffa657', '#ff7b72']
const KIND_ABBREV: Record<string, string> = { Class: 'C', Interface: 'I', Record: 'R', Enum: 'E', Method: 'M' }
const NODE_R = 20
const MAX_NODES = 80
const MAX_ITER = 350

function repoColor(name: string): string {
  let h = 0
  for (let i = 0; i < name.length; i++) { h = ((h << 5) - h) + name.charCodeAt(i); h |= 0 }
  return REPO_PALETTE[Math.abs(h) % REPO_PALETTE.length]
}

interface NodeSim { id: string; label: string; kind: string; repo: string; weight: number; x: number; y: number; vx: number; vy: number }
interface EdgeSim { srcId: string; tgtId: string; kind: string; cross: boolean }

interface Props { result: CrossRepoSubgraphResponse }

export function GraphCanvas({ result }: Props) {
  const svgRef = useRef<SVGSVGElement>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const nodesRef = useRef<NodeSim[]>([])
  const edgesRef = useRef<EdgeSim[]>([])
  const rafRef = useRef(0)
  const iterRef = useRef(0)

  const [renderNodes, setRenderNodes] = useState<NodeSim[]>([])
  const [viewBox, setViewBox] = useState({ x: -50, y: -50, w: 900, h: 700 })
  const [panOrigin, setPanOrigin] = useState<{ x: number; y: number } | null>(null)
  const [selected, setSelected] = useState<string | null>(null)
  const [hovered, setHovered] = useState<string | null>(null)

  useEffect(() => {
    if (result.nodes.length === 0 || result.nodes.length > MAX_NODES) return

    const w = svgRef.current?.clientWidth ?? 800
    const h = svgRef.current?.clientHeight ?? 600
    const cx = w / 2, cy = h / 2
    const angle = (2 * Math.PI) / result.nodes.length
    const radius = Math.min(cx, cy) * 0.7

    nodesRef.current = result.nodes.map((n, i) => ({
      id: `${n.repositoryName}::${n.identifier}`,
      label: n.name,
      kind: n.kind,
      repo: n.repositoryName,
      weight: n.weight,
      x: cx + radius * Math.cos(angle * i),
      y: cy + radius * Math.sin(angle * i),
      vx: 0, vy: 0,
    }))

    const intra: EdgeSim[] = result.edges.map(e => ({
      srcId: `${e.repositoryName}::${e.sourceIdentifier}`,
      tgtId: `${e.repositoryName}::${e.targetIdentifier}`,
      kind: e.kind, cross: false,
    }))
    const cross: EdgeSim[] = result.crossRepoLinks.map(l => ({
      srcId: `${l.sourceRepositoryName}::${l.sourceIdentifier}`,
      tgtId: `${l.targetRepositoryName}::${l.targetIdentifier}`,
      kind: l.kind, cross: true,
    }))
    const nodeIds = new Set(nodesRef.current.map(n => n.id))
    edgesRef.current = [...intra, ...cross].filter(e => nodeIds.has(e.srcId) && nodeIds.has(e.tgtId))

    iterRef.current = 0
    setRenderNodes([...nodesRef.current])
    setSelected(null)

    cancelAnimationFrame(rafRef.current)

    const tick = () => {
      if (iterRef.current >= MAX_ITER) return
      const ns = nodesRef.current
      const es = edgesRef.current
      const REPULSION = 5000, SPRING_K = 0.06, IDEAL = 190, DAMP = 0.82, GRAVITY = 0.003

      const fx = new Float64Array(ns.length)
      const fy = new Float64Array(ns.length)

      for (let i = 0; i < ns.length; i++) {
        for (let j = i + 1; j < ns.length; j++) {
          const dx = ns[j].x - ns[i].x, dy = ns[j].y - ns[i].y
          const d = Math.sqrt(dx * dx + dy * dy) || 1
          const f = REPULSION / (d * d)
          const nx = dx / d * f, ny = dy / d * f
          fx[i] -= nx; fy[i] -= ny; fx[j] += nx; fy[j] += ny
        }
      }

      for (const e of es) {
        const si = ns.findIndex(n => n.id === e.srcId)
        const ti = ns.findIndex(n => n.id === e.tgtId)
        if (si < 0 || ti < 0) continue
        const dx = ns[ti].x - ns[si].x, dy = ns[ti].y - ns[si].y
        const d = Math.sqrt(dx * dx + dy * dy) || 1
        const f = SPRING_K * (d - IDEAL)
        const nx = dx / d * f, ny = dy / d * f
        fx[si] += nx; fy[si] += ny; fx[ti] -= nx; fy[ti] -= ny
      }

      for (let i = 0; i < ns.length; i++) {
        fx[i] += (cx - ns[i].x) * GRAVITY
        fy[i] += (cy - ns[i].y) * GRAVITY
        ns[i].vx = (ns[i].vx + fx[i]) * DAMP
        ns[i].vy = (ns[i].vy + fy[i]) * DAMP
        ns[i].x += ns[i].vx
        ns[i].y += ns[i].vy
      }

      iterRef.current++
      setRenderNodes([...ns])
      rafRef.current = requestAnimationFrame(tick)
    }

    rafRef.current = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(rafRef.current)
  }, [result])

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

  // Bloquear zoom del navegador sobre el grafo — listener nativo con passive:false
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

  const nodeMap = new Map(renderNodes.map(n => [n.id, n]))
  const highlightIds: Set<string> | null = selected
    ? new Set([selected, ...edgesRef.current.filter(e => e.srcId === selected || e.tgtId === selected).flatMap(e => [e.srcId, e.tgtId])])
    : null

  return (
    <div ref={containerRef} className="w-full h-full overflow-hidden">
    <svg ref={svgRef} className="w-full h-full select-none"
      style={{ cursor: panOrigin ? 'grabbing' : 'grab' }}
      viewBox={`${viewBox.x} ${viewBox.y} ${viewBox.w} ${viewBox.h}`}
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

      {edgesRef.current.map((edge, i) => {
        const src = nodeMap.get(edge.srcId), tgt = nodeMap.get(edge.tgtId)
        if (!src || !tgt) return null
        const dx = tgt.x - src.x, dy = tgt.y - src.y
        const d = Math.sqrt(dx * dx + dy * dy) || 1
        const r = NODE_R + 3
        const x1 = src.x + dx / d * r, y1 = src.y + dy / d * r
        const x2 = tgt.x - dx / d * (r + 8), y2 = tgt.y - dy / d * (r + 8)
        const dim = highlightIds && !(highlightIds.has(edge.srcId) && highlightIds.has(edge.tgtId))
        return (
          <line key={i} x1={x1} y1={y1} x2={x2} y2={y2}
            stroke={edge.cross ? '#e3b341' : '#3d444d'}
            strokeWidth={edge.cross ? 2 : 1.5}
            strokeDasharray={edge.cross ? '7,3' : undefined}
            strokeOpacity={dim ? 0.1 : 0.85}
            markerEnd={`url(#arr-${edge.cross ? 'x' : 'i'})`}
          />
        )
      })}

      {renderNodes.map(node => {
        const color = repoColor(node.repo)
        const r = NODE_R + Math.min(node.weight, 6)
        const dim = highlightIds && !highlightIds.has(node.id)
        const isSel = selected === node.id
        const isHov = hovered === node.id
        return (
          <g key={node.id} className="graph-node" transform={`translate(${node.x},${node.y})`}
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
