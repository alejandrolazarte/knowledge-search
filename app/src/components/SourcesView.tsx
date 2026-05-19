import { useEffect, useRef, useState } from 'react'
import type { SourceConfigurationFile, SourceDefinition, SourceKind } from '../types'

const DEFAULT_EXCLUDES = ['**/.git/**', '**/node_modules/**', '**/bin/**', '**/obj/**', '**/dist/**', '**/build/**', '**/.next/**', '**/coverage/**']
const REPOSITORY_DOC_INCLUDES = ['README.md', 'docs/**/*.md', 'specs/**/*.md', 'adr/**/*.md']
const KNOWLEDGE_DOC_INCLUDES = ['**/*.md', '**/*.mdx']
const CODE_INCLUDES = ['**/*.cs', '**/*.ts', '**/*.tsx', '**/*.js', '**/*.jsx', '**/*.py']

function lines(value: string[]): string {
  return value.join('\n')
}

function splitLines(value: string): string[] {
  return value.split(/\r?\n/).map(line => line.trim()).filter(Boolean)
}

function createSource(kind: SourceKind, count: number): SourceDefinition {
  const id = `source-${count + 1}`
  return {
    id,
    name: id,
    kind,
    hostPath: '',
    indexCode: kind === 'Repository',
    indexDocs: true,
    docIncludes: kind === 'Repository' ? REPOSITORY_DOC_INCLUDES : KNOWLEDGE_DOC_INCLUDES,
    codeIncludes: CODE_INCLUDES,
    excludes: DEFAULT_EXCLUDES,
  }
}

export function SourcesView() {
  const [config, setConfig] = useState<SourceConfigurationFile | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [loadError, setLoadError] = useState('')
  const [status, setStatus] = useState('')
  const [saving, setSaving] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    fetch('/sources')
      .then(async response => {
        if (!response.ok) throw new Error(await response.text())
        return response.json() as Promise<SourceConfigurationFile>
      })
      .then(next => {
        setConfig(next)
        setSelectedId(next.sources[0]?.id ?? null)
      })
      .catch(() => setLoadError('No se pudo cargar la configuración de sources.'))
  }, [])

  if (loadError) {
    return <div className="flex-1 p-4 text-sm text-red-400">{loadError}</div>
  }

  if (!config) {
    return <div className="flex-1 p-4 text-sm text-gh-muted">Cargando sources…</div>
  }

  const selected = config.sources.find(source => source.id === selectedId) ?? config.sources[0] ?? null

  const updateSelected = (patch: Partial<SourceDefinition>) => {
    if (!selected) return
    setConfig(prev => prev
      ? { ...prev, sources: prev.sources.map(source => source.id === selected.id ? { ...source, ...patch } : source) }
      : prev)
    setStatus('Unsaved changes')
  }

  const addSource = (kind: SourceKind) => {
    setConfig(prev => {
      const base = prev ?? { version: 1, sources: [] }
      const source = createSource(kind, base.sources.length)
      setSelectedId(source.id)
      setStatus('Unsaved changes')
      return { ...base, sources: [...base.sources, source] }
    })
  }

  const deleteSelected = () => {
    if (!selected) return
    setConfig(prev => {
      if (!prev) return prev
      const sources = prev.sources.filter(source => source.id !== selected.id)
      setSelectedId(sources[0]?.id ?? null)
      setStatus('Unsaved changes')
      return { ...prev, sources }
    })
  }

  const save = async () => {
    if (!config) return
    setSaving(true)
    setStatus('Saving…')
    try {
      const response = await fetch('/sources', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(config),
      })
      if (!response.ok) {
        const body = await response.json().catch(() => null) as { error?: string } | null
        throw new Error(body?.error ?? 'Error al guardar')
      }
      const saved = await response.json() as { configuration: SourceConfigurationFile, jobIds: string[] }
      setConfig(saved.configuration)
      setStatus('Saved. Restart or reindex flow required for active index roots.')
    } catch (err) {
      setStatus(err instanceof Error ? err.message : 'Error al guardar')
    } finally {
      setSaving(false)
    }
  }

  const importJson = () => fileInputRef.current?.click()

  const onImportFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    try {
      const parsed = JSON.parse(await file.text()) as SourceConfigurationFile
      if (!Array.isArray(parsed.sources)) throw new Error('sources debe ser un array')
      setConfig(parsed)
      setSelectedId(parsed.sources[0]?.id ?? null)
      setStatus('Imported JSON. Save changes to persist.')
    } catch {
      setStatus('JSON inválido')
    }
  }

  const exportJson = () => {
    if (!config) return
    const blob = new Blob([JSON.stringify(config, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'sources.json'
    link.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="flex-1 flex flex-col overflow-hidden p-4 gap-3">
      <input ref={fileInputRef} type="file" accept="application/json,.json" className="hidden" onChange={onImportFile} />
      <div className="flex items-center gap-2 shrink-0">
        <button onClick={() => addSource('Repository')} className="bg-gh-accent hover:opacity-90 text-white text-sm px-3 py-1.5 rounded font-medium">
          Add folder
        </button>
        <button onClick={importJson} className="border border-gh-border bg-gh-surface text-gh-muted hover:text-gh-text text-sm px-3 py-1.5 rounded">
          Import
        </button>
        <button onClick={exportJson} className="border border-gh-border bg-gh-surface text-gh-muted hover:text-gh-text text-sm px-3 py-1.5 rounded">
          Export
        </button>
        <div className="flex-1" />
        {status && <span className="text-xs text-gh-muted">{status}</span>}
      </div>
      <div className="flex-1 min-h-0 grid grid-cols-[320px_minmax(0,1fr)] border border-gh-border rounded-lg overflow-hidden bg-gh-bg">
        <div className="border-r border-gh-border bg-gh-surface/40 overflow-y-auto">
          {config.sources.map(source => (
            <SourceListItem key={source.id} source={source} active={source.id === selectedId} onClick={() => setSelectedId(source.id)} />
          ))}
          {config.sources.length === 0 && (
            <div className="p-4 text-xs text-gh-muted">No sources. Add a folder to get started.</div>
          )}
        </div>
        <div className="overflow-y-auto">
          {selected
            ? <SourceEditor source={selected} onChange={updateSelected} onDelete={deleteSelected} onSave={save} saving={saving} />
            : <EmptyState />}
        </div>
      </div>
    </div>
  )
}

function SourceListItem({ source, active, onClick }: {
  source: SourceDefinition
  active: boolean
  onClick: () => void
}) {
  return (
    <button onClick={onClick}
      className={`block w-full text-left border-b border-gh-border p-3 transition-colors ${active ? 'bg-gh-card' : 'hover:bg-gh-surface'}`}>
      <div className="flex items-center gap-2">
        <span className="text-[10px] uppercase border border-gh-border rounded-full px-1.5 py-0.5 text-gh-muted shrink-0">{source.kind}</span>
        <span className="text-sm font-medium text-gh-text truncate">{source.name}</span>
      </div>
      <div className="text-xs text-gh-muted truncate mt-1">{source.hostPath || 'No path set'}</div>
      <div className="flex gap-1 mt-2">
        <span className={`text-[10px] rounded-full px-1.5 py-0.5 border ${source.indexCode ? 'text-green-400 border-green-500/40' : 'text-gh-muted border-gh-border'}`}>code</span>
        <span className={`text-[10px] rounded-full px-1.5 py-0.5 border ${source.indexDocs ? 'text-green-400 border-green-500/40' : 'text-gh-muted border-gh-border'}`}>docs</span>
      </div>
    </button>
  )
}

function SourceEditor({ source, onChange, onDelete, onSave, saving }: {
  source: SourceDefinition
  onChange: (patch: Partial<SourceDefinition>) => void
  onDelete: () => void
  onSave: () => void
  saving: boolean
}) {
  return (
    <div className="p-4 max-w-3xl">
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Name" value={source.name} onChange={name => onChange({ name })} />
        <TextField label="ID" value={source.id} onChange={id => onChange({ id })} />
      </div>
      <TextField label="Host path" value={source.hostPath} onChange={hostPath => onChange({ hostPath })} />
      <div className="flex gap-2 my-3">
        <button
          onClick={() => onChange({ kind: 'Knowledge', indexCode: false, indexDocs: true, docIncludes: KNOWLEDGE_DOC_INCLUDES })}
          className={kindButton(source.kind === 'Knowledge')}>
          Knowledge
        </button>
        <button
          onClick={() => onChange({ kind: 'Repository', indexCode: true, indexDocs: true, docIncludes: REPOSITORY_DOC_INCLUDES })}
          className={kindButton(source.kind === 'Repository')}>
          Repository
        </button>
      </div>
      <div className="grid grid-cols-2 gap-3 my-3">
        <Toggle
          label="Index code"
          description="Repo Search + Code Graph"
          checked={source.indexCode}
          onChange={indexCode => onChange({ indexCode })}
        />
        <Toggle
          label="Index docs"
          description="Knowledge Search"
          checked={source.indexDocs}
          onChange={indexDocs => onChange({ indexDocs })}
        />
      </div>
      <RulesField label="Doc includes" value={source.docIncludes} onChange={docIncludes => onChange({ docIncludes })} />
      <RulesField label="Code includes" value={source.codeIncludes} onChange={codeIncludes => onChange({ codeIncludes })} />
      <RulesField label="Excludes" value={source.excludes} onChange={excludes => onChange({ excludes })} />
      <div className="flex gap-2 pt-2">
        <button onClick={onSave} disabled={saving} className="bg-gh-accent text-white text-sm px-3 py-1.5 rounded disabled:opacity-50">
          Save changes
        </button>
        <button onClick={onDelete} className="border border-gh-border text-red-400 hover:bg-red-500/10 text-sm px-3 py-1.5 rounded">
          Delete
        </button>
      </div>
    </div>
  )
}

function TextField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="block mb-3">
      <span className="block text-[10px] uppercase tracking-widest text-gh-muted mb-1">{label}</span>
      <input
        value={value}
        onChange={event => onChange(event.target.value)}
        className="w-full bg-gh-surface border border-gh-border rounded px-2 py-1.5 text-sm text-gh-text outline-none focus:border-gh-accent"
      />
    </label>
  )
}

function Toggle({ label, description, checked, onChange }: {
  label: string
  description: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <button
      onClick={() => onChange(!checked)}
      className={`text-left border rounded p-3 transition-colors ${checked ? 'border-gh-accent bg-gh-accent/10' : 'border-gh-border bg-gh-surface'}`}>
      <span className="block text-sm font-medium text-gh-text">{label}</span>
      <span className="block text-xs text-gh-muted mt-0.5">{description}</span>
    </button>
  )
}

function RulesField({ label, value, onChange }: {
  label: string
  value: string[]
  onChange: (value: string[]) => void
}) {
  return (
    <label className="block mb-3">
      <span className="block text-[10px] uppercase tracking-widest text-gh-muted mb-1">{label}</span>
      <textarea
        value={lines(value)}
        onChange={event => onChange(splitLines(event.target.value))}
        className="w-full min-h-24 bg-gh-surface border border-gh-border rounded px-2 py-1.5 font-mono text-xs text-gh-text outline-none focus:border-gh-accent"
      />
    </label>
  )
}

function EmptyState() {
  return <div className="p-6 text-sm text-gh-muted">No source selected.</div>
}

function kindButton(active: boolean): string {
  return `flex-1 border rounded px-3 py-2 text-sm transition-colors ${active ? 'border-gh-accent bg-gh-accent/10 text-gh-text' : 'border-gh-border bg-gh-surface text-gh-muted hover:text-gh-text'}`
}
