import { useState, useEffect, useRef } from 'react'
import { Sidebar }    from './components/Sidebar'
import { SearchView } from './components/SearchView'
import { SkillsView } from './components/SkillsView'
import { SkillPanel } from './components/SkillPanel'
import { FilePanel }  from './components/FilePanel'
import { GraphView }  from './components/GraphView'
import { RepoSearchView } from './components/RepoSearchView'
import { useTheme }    from './hooks/useTheme'
import { useFontSize } from './hooks/useFontSize'
import type { Skill } from './types'

type View = 'search' | 'repo-search' | 'skills' | 'graph'

interface ActiveFile {
  path:     string
  endpoint: string
}

export function App() {
  const { theme, setTheme }                       = useTheme()
  const { size: fontSize, setSize: setFontSize }  = useFontSize()
  const [view,        setView]                    = useState<View>('search')
  const [activeSkill, setActiveSkill]             = useState<Skill | null>(null)
  const [activeFile,  setActiveFile]              = useState<ActiveFile | null>(null)
  const [reindexing,  setReindexing]              = useState(false)
  const [statusMsg,   setStatusMsg]               = useState('')
  const searchInputRef                            = useRef<HTMLInputElement>(null)

  // Opening a file closes any skill (and vice versa) so only one right panel is active
  const openFile = (path: string, endpoint: string = '/file') => {
    setActiveSkill(null)
    setActiveFile({ path, endpoint })
  }
  const openSkill = (skill: Skill | null) => {
    setActiveFile(null)
    setActiveSkill(skill)
  }

  // Ctrl+K — focus search from anywhere
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault()
        if (view !== 'search') setView('search')
        setTimeout(() => {
          searchInputRef.current?.focus()
          searchInputRef.current?.select()
        }, 10)
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [view])

  useEffect(() => {
    fetch('/index', { method: 'POST' }).catch(() => {})
  }, [])

  const handleReindex = async () => {
    setReindexing(true)
    setStatusMsg('Re-indexando…')
    try {
      const d = await fetch('/index', { method: 'POST' }).then(r => r.json())
      setStatusMsg(`+${d.added} nuevos  ~${d.updated} actualizados  −${d.deleted} eliminados`)
    } catch {
      setStatusMsg('Error al re-indexar')
    } finally {
      setReindexing(false)
    }
  }

  return (
    <div className="h-screen flex overflow-hidden bg-gh-bg text-gh-text">
      <Sidebar
        view={view}
        onView={v => { setView(v); setStatusMsg('') }}
        theme={theme}
        onTheme={setTheme}
        fontSize={fontSize}
        onFontSize={setFontSize}
      />

      <div className="flex-1 flex flex-col overflow-hidden min-w-0">
        <header className="h-10 border-b border-gh-border flex items-center px-4 gap-3 shrink-0">
          <span className="text-sm font-medium">
            {view === 'search' ? 'Knowledge Search' : view === 'repo-search' ? 'Repo Search' : view === 'skills' ? 'Skills' : 'Code Graph'}
          </span>
          <div className="flex-1" />
          {view === 'search' && (
            <button
              onClick={handleReindex}
              disabled={reindexing}
              className="flex items-center gap-1.5 text-xs text-gh-muted hover:text-gh-text
                border border-gh-border rounded px-2 py-1 disabled:opacity-50"
            >
              <svg className={`w-3 h-3 ${reindexing ? 'animate-spin' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round"
                  d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99"/>
              </svg>
              Re-index
            </button>
          )}
        </header>

        {view === 'search' && (
          <SearchView statusMsg={statusMsg} onStatus={setStatusMsg} inputRef={searchInputRef} onOpenFile={openFile} />
        )}
        {view === 'repo-search' && (
          <RepoSearchView onOpenFile={openFile} />
        )}
        {view === 'skills' && (
          <SkillsView onOpen={openSkill} active={activeSkill} />
        )}
        {view === 'graph' && (
          <GraphView />
        )}
      </div>

      <FilePanel
        path={activeFile?.path ?? null}
        endpoint={activeFile?.endpoint}
        onClose={() => setActiveFile(null)}
      />
      <SkillPanel skill={activeSkill} onClose={() => setActiveSkill(null)} />
    </div>
  )
}
