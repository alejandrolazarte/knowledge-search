import { useState, useEffect, useRef } from 'react'
import type { LogEvent } from '../types'

const TYPE_STYLE: Record<LogEvent['type'], string> = {
  added:   'bg-green-500/20 text-green-400',
  updated: 'bg-blue-500/20  text-blue-400',
  deleted: 'bg-red-500/20   text-red-400',
}

interface Props {
  collapsed: boolean
}

export function EventLog({ collapsed }: Props) {
  const [events,  setEvents]  = useState<LogEvent[]>([])
  const [unread,  setUnread]  = useState(0)
  const [open,    setOpen]    = useState(false)
  const wrapRef = useRef<HTMLDivElement>(null)

  // Carga historial al iniciar
  useEffect(() => {
    fetch('/log')
      .then(r => r.json())
      .then((data: LogEvent[]) => setEvents(data))
      .catch(() => {})
  }, [])

  // SSE: eventos en tiempo real
  useEffect(() => {
    const syncLog = () =>
      fetch('/log')
        .then(r => r.json())
        .then((data: LogEvent[]) => setEvents(data))
        .catch(() => {})

    const es = new EventSource('/events')

    es.onmessage = e => {
      const ev: LogEvent = JSON.parse(e.data)
      setEvents(prev => [...prev.slice(-99), ev])
      setUnread(n => n + 1)
    }

    // Al reconectar re-sincroniza el log para no perder eventos ocurridos durante el corte
    es.onerror = () => syncLog()

    return () => es.close()
  }, [])

  // Cierra al hacer click fuera
  useEffect(() => {
    if (!open) return
    const handler = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node))
        setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open])

  const toggle = () => {
    setOpen(o => !o)
    setUnread(0)
  }

  return (
    <div ref={wrapRef} className="relative">
      <button
        onClick={toggle}
        title="Log de indexación"
        className={`flex items-center gap-2.5 px-2 py-1.5 rounded w-full transition-colors
          ${collapsed ? 'justify-center' : ''}
          ${open ? 'bg-gh-card text-gh-text' : 'text-gh-muted hover:text-gh-text hover:bg-gh-surface'}`}
      >
        <span className="relative shrink-0">
          <BellIcon />
          {unread > 0 && (
            <span className="absolute -top-1 -right-1 bg-gh-accent text-white text-[8px] font-bold rounded-full w-3 h-3 flex items-center justify-center leading-none">
              {unread > 9 ? '9+' : unread}
            </span>
          )}
        </span>
        {!collapsed && <span className="text-sm">Actividad</span>}
      </button>

      {open && (
        <div className="absolute left-full top-0 ml-1 w-72 bg-gh-surface border border-gh-border rounded-lg shadow-xl z-30 overflow-hidden">
          <div className="px-3 py-2 border-b border-gh-border flex items-center justify-between">
            <span className="text-xs font-medium text-gh-text">Log de indexación</span>
            <span className="text-[10px] text-gh-muted">{events.length} eventos</span>
          </div>

          <div className="max-h-72 overflow-y-auto">
            {events.length === 0 ? (
              <p className="text-xs text-gh-muted px-3 py-6 text-center">Sin eventos aún</p>
            ) : (
              [...events].reverse().map((ev, i) => (
                <div key={i} className="flex items-center gap-2 px-3 py-1.5 border-b border-gh-border/40 last:border-0 hover:bg-gh-card">
                  <span className={`text-[10px] font-mono shrink-0 px-1 rounded ${TYPE_STYLE[ev.type]}`}>
                    {ev.type}
                  </span>
                  <span className="text-[11px] text-gh-muted font-mono truncate flex-1" title={ev.path}>
                    {ev.path}
                  </span>
                  <span className="text-[10px] text-gh-border shrink-0">
                    {new Date(ev.ts).toLocaleTimeString()}
                  </span>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}

function BellIcon() {
  return (
    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
      <path strokeLinecap="round" strokeLinejoin="round"
        d="M14.857 17.082a23.848 23.848 0 005.454-1.31A8.967 8.967 0 0118 9.75v-.7V9A6 6 0 006 9v.75a8.967 8.967 0 01-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 01-5.714 0m5.714 0a3 3 0 11-5.714 0"/>
    </svg>
  )
}
