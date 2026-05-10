import { useState, useEffect } from 'react'
import { THEMES } from '../hooks/useTheme'
import type { Theme } from '../hooks/useTheme'
import type { FontSize } from '../hooks/useFontSize'
import { EventLog } from './EventLog'

type View = 'search' | 'skills' | 'graph'

interface Props {
  view:      View
  onView:    (v: View) => void
  theme:     Theme
  onTheme:   (t: Theme) => void
  fontSize:  FontSize
  onFontSize:(s: FontSize) => void
}

const NAV_KEY = 'nav-collapsed'

const FONT_LABELS: Record<FontSize, string> = { sm: 'S', md: 'M', lg: 'L', xl: 'XL' }

export function Sidebar({ view, onView, theme, onTheme, fontSize, onFontSize }: Props) {
  const currentTheme = THEMES.find(t => t.id === theme)!
  const [collapsed, setCollapsed] = useState(
    () => localStorage.getItem(NAV_KEY) === 'true'
  )

  useEffect(() => {
    localStorage.setItem(NAV_KEY, String(collapsed))
  }, [collapsed])

  return (
    <aside className={`flex flex-col shrink-0 bg-gh-bg border-r border-gh-border transition-all duration-150 ${collapsed ? 'w-12' : 'w-52'}`}>
      <div className="flex items-center gap-2 px-3 py-3 border-b border-gh-border min-h-[40px]">
        <span className="text-base shrink-0">🛠</span>
        {!collapsed && <span className="font-semibold text-sm text-gh-text flex-1">Dev Tools</span>}
        <button
          onClick={() => setCollapsed(c => !c)}
          className="text-gh-muted hover:text-gh-text ml-auto"
          title={collapsed ? 'Expandir' : 'Colapsar'}
        >
          <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round"
              d={collapsed ? 'M9 5l7 7-7 7' : 'M15 19l-7-7 7-7'} />
          </svg>
        </button>
      </div>

      <nav className="flex-1 py-3 px-2 space-y-0.5">
        {!collapsed && (
          <p className="text-[10px] uppercase tracking-widest text-gh-muted px-2 mb-2">Knowledge</p>
        )}
        <NavItem icon={<SearchIcon />} label="Search"
          active={view === 'search'} collapsed={collapsed} onClick={() => onView('search')} />
        <NavItem icon={<SkillsIcon />} label="Skills"
          active={view === 'skills'} collapsed={collapsed} onClick={() => onView('skills')} />
        <NavItem icon={<GraphIcon />} label="Code Graph"
          active={view === 'graph'} collapsed={collapsed} onClick={() => onView('graph')} />

        <div className="pt-1 mt-1 border-t border-gh-border/50">
          <EventLog collapsed={collapsed} />
        </div>
      </nav>

      <div className="px-2 py-3 border-t border-gh-border space-y-1">
        {/* Font size */}
        {collapsed ? (
          <button
            onClick={() => {
              const order: FontSize[] = ['sm', 'md', 'lg', 'xl']
              onFontSize(order[(order.indexOf(fontSize) + 1) % order.length])
            }}
            title={`Fuente: ${FONT_LABELS[fontSize]}`}
            className="flex items-center justify-center w-full px-2 py-1.5 text-xs font-bold text-gh-muted hover:text-gh-text rounded"
          >
            A
          </button>
        ) : (
          <div className="flex items-center gap-1 px-2 py-1">
            <span className="text-xs text-gh-muted mr-1">A</span>
            {(['sm', 'md', 'lg', 'xl'] as FontSize[]).map(s => (
              <button key={s} onClick={() => onFontSize(s)} title={s}
                className={`flex-1 py-0.5 rounded text-xs font-medium transition-colors
                  ${fontSize === s
                    ? 'bg-gh-accent text-white'
                    : 'bg-gh-surface text-gh-muted hover:text-gh-text hover:bg-gh-card border border-gh-border'}`}
              >{FONT_LABELS[s]}</button>
            ))}
          </div>
        )}

        {/* Theme picker */}
        {collapsed ? (
          <button
            onClick={() => {
              const idx = THEMES.findIndex(t => t.id === theme)
              onTheme(THEMES[(idx + 1) % THEMES.length].id)
            }}
            title={`Tema: ${currentTheme.label}`}
            className="flex items-center justify-center w-full px-2 py-1.5 text-gh-muted hover:text-gh-text rounded"
          >
            <span className="w-3 h-3 rounded-full border border-gh-border" style={{ background: currentTheme.accent }} />
          </button>
        ) : (
          <div className="px-2 py-1 space-y-0.5">
            <p className="text-[10px] text-gh-muted mb-1">Tema</p>
            {THEMES.map(t => (
              <button key={t.id} onClick={() => onTheme(t.id)}
                className={`flex items-center gap-2 w-full px-2 py-1 rounded text-xs transition-colors
                  ${theme === t.id
                    ? 'bg-gh-card text-gh-text'
                    : 'text-gh-muted hover:text-gh-text hover:bg-gh-surface'}`}
              >
                <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ background: t.accent }} />
                {t.label}
              </button>
            ))}
          </div>
        )}
      </div>
    </aside>
  )
}

function NavItem({ icon, label, active, collapsed, onClick }: {
  icon: React.ReactNode; label: string; active: boolean; collapsed: boolean; onClick: () => void
}) {
  return (
    <button onClick={onClick} title={collapsed ? label : undefined}
      className={`flex items-center gap-2.5 px-2 py-1.5 rounded text-sm w-full transition-colors
        ${collapsed ? 'justify-center' : ''}
        ${active ? 'bg-gh-card text-gh-accent' : 'text-gh-muted hover:text-gh-text hover:bg-gh-surface'}`}
    >
      {icon}
      {!collapsed && <span>{label}</span>}
    </button>
  )
}

function SearchIcon() {
  return <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
    <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 15.803 7.5 7.5 0 0015.803 15.803z"/>
  </svg>
}
function SkillsIcon() {
  return <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
    <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z"/>
  </svg>
}
function GraphIcon() {
  return <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
    <path strokeLinecap="round" strokeLinejoin="round" d="M7.5 14.25v2.25m3-4.5v4.5m3-6.75v6.75m3-9v9M6 20.25h12A2.25 2.25 0 0020.25 18V6A2.25 2.25 0 0018 3.75H6A2.25 2.25 0 003.75 6v12A2.25 2.25 0 006 20.25z"/>
  </svg>
}
