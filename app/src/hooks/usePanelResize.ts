import { useEffect, useRef, useState } from 'react'

const KEY = 'devtools-panel-width'
const MIN = 320

export function usePanelResize() {
  const [width, setWidth] = useState<number>(() => {
    const saved = parseInt(localStorage.getItem(KEY) ?? '0', 10)
    return saved >= MIN ? saved : 480
  })
  const dragging = useRef(false)
  const startX   = useRef(0)
  const startW   = useRef(0)

  const onMouseDown = (e: React.MouseEvent) => {
    e.preventDefault()
    dragging.current = true
    startX.current   = e.clientX
    startW.current   = width
    document.body.style.userSelect = 'none'
    document.body.style.cursor     = 'ew-resize'
  }

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!dragging.current) return
      const next = Math.max(MIN, Math.min(window.innerWidth * 0.9,
        startW.current - (e.clientX - startX.current)))
      setWidth(next)
    }
    const onUp = () => {
      if (!dragging.current) return
      dragging.current = false
      document.body.style.userSelect = ''
      document.body.style.cursor     = ''
      localStorage.setItem(KEY, String(width))
    }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
    }
  }, [width])

  return { width, onMouseDown }
}
