import { useState, useEffect } from 'react'

export type FontSize = 'sm' | 'md' | 'lg' | 'xl'

const KEY = 'devtools-font-size'
const SIZES: Record<FontSize, string> = {
  sm: '12px',
  md: '14px',
  lg: '16px',
  xl: '18px',
}

export function useFontSize() {
  const [size, setSize] = useState<FontSize>(
    () => (localStorage.getItem(KEY) as FontSize) ?? 'md'
  )

  useEffect(() => {
    document.documentElement.style.fontSize = SIZES[size]
    localStorage.setItem(KEY, size)
  }, [size])

  return { size, setSize, sizes: Object.keys(SIZES) as FontSize[] }
}
