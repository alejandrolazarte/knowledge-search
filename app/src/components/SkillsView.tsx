import { useState, useEffect } from 'react'
import type { Skill } from '../types'

interface Props {
  onOpen:  (skill: Skill) => void
  active:  Skill | null
}

export function SkillsView({ onOpen, active }: Props) {
  const [allSkills, setAllSkills] = useState<Skill[]>([])
  const [filter,    setFilter]    = useState('')
  const [loading,   setLoading]   = useState(true)
  const [error,     setError]     = useState(false)

  useEffect(() => {
    fetch('/skills')
      .then(r => r.json())
      .then((data: Skill[]) => { setAllSkills(data); setLoading(false) })
      .catch(() => { setError(true); setLoading(false) })
  }, [])

  const visible = filter
    ? allSkills.filter(s =>
        s.name.toLowerCase().includes(filter.toLowerCase()) ||
        (s.description ?? '').toLowerCase().includes(filter.toLowerCase()))
    : allSkills

  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 gap-3">
      <div className="flex items-center gap-3">
        <input
          value={filter}
          onChange={e => setFilter(e.target.value)}
          placeholder="Filtrar skills…"
          className="flex-1 bg-gh-surface border border-gh-border rounded px-3 py-1.5 text-sm text-gh-text
            outline-none focus:border-gh-accent placeholder-gh-muted"
        />
        <span className="text-xs text-gh-muted shrink-0">{visible.length} skills</span>
      </div>

      <div className="flex-1 overflow-y-auto">
        {loading && <p className="text-gh-muted text-sm">Cargando…</p>}
        {error   && <p className="text-red-400 text-sm">Error al cargar skills</p>}
        {!loading && !error && visible.length === 0 && (
          <p className="text-gh-muted text-sm">Sin resultados</p>
        )}
        <div className="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-2">
          {visible.map(s => (
            <button
              key={s.dirName}
              onClick={() => onOpen(s)}
              className={`text-left p-3 rounded-lg border transition-colors
                ${active?.dirName === s.dirName
                  ? 'border-gh-accent bg-gh-card'
                  : 'border-gh-border bg-gh-surface hover:bg-gh-card'}`}
            >
              <div className="text-base mb-1">🔧</div>
              <div className="text-sm font-medium text-gh-text leading-snug mb-1">{s.name}</div>
              <div className="text-xs text-gh-muted line-clamp-2">{s.description}</div>
              <div className="text-[10px] text-gh-border mt-1.5 font-mono truncate">{s.dirName}</div>
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
