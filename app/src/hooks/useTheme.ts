import { useState, useEffect } from 'react'

export type Theme = 'github-dark' | 'vscode-dark' | 'dracula' | 'github-light' | 'visual-studio' | 'jetbrains' | 'vs-dark'

export const THEMES: { id: Theme; label: string; dark: boolean; accent: string }[] = [
  { id: 'github-dark',   label: 'GitHub Dark',        dark: true,  accent: '#2f81f7' },
  { id: 'vscode-dark',   label: 'VS Code Dark',       dark: true,  accent: '#569cd6' },
  { id: 'vs-dark',       label: 'Visual Studio Dark', dark: true,  accent: '#0078d4' },
  { id: 'dracula',       label: 'Dracula',            dark: true,  accent: '#bd93f9' },
  { id: 'jetbrains',     label: 'JetBrains',          dark: true,  accent: '#6897bb' },
  { id: 'github-light',  label: 'GitHub Light',       dark: false, accent: '#0969da' },
  { id: 'visual-studio', label: 'Visual Studio',      dark: false, accent: '#0078d4' },
]

const KEY = 'devtools-theme'

export function useTheme() {
  const [theme, setTheme] = useState<Theme>(() => {
    const stored = localStorage.getItem(KEY) as Theme
    return THEMES.find(t => t.id === stored) ? stored : 'github-dark'
  })

  useEffect(() => {
    const t = THEMES.find(t => t.id === theme) ?? THEMES[0]
    document.documentElement.setAttribute('data-theme', theme)
    document.documentElement.classList.toggle('dark', t.dark)
    localStorage.setItem(KEY, theme)
  }, [theme])

  return { theme, setTheme }
}
