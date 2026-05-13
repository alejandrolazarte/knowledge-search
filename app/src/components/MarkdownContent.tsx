import React from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Light as SyntaxHighlighter } from 'react-syntax-highlighter'
import { githubGist } from 'react-syntax-highlighter/dist/esm/styles/hljs'
import sql    from 'react-syntax-highlighter/dist/esm/languages/hljs/sql'
import csharp from 'react-syntax-highlighter/dist/esm/languages/hljs/csharp'
import bash   from 'react-syntax-highlighter/dist/esm/languages/hljs/bash'
import json   from 'react-syntax-highlighter/dist/esm/languages/hljs/json'
import xml    from 'react-syntax-highlighter/dist/esm/languages/hljs/xml'
import ts     from 'react-syntax-highlighter/dist/esm/languages/hljs/typescript'
import js     from 'react-syntax-highlighter/dist/esm/languages/hljs/javascript'
import yaml   from 'react-syntax-highlighter/dist/esm/languages/hljs/yaml'
import python from 'react-syntax-highlighter/dist/esm/languages/hljs/python'

SyntaxHighlighter.registerLanguage('sql',        sql)
SyntaxHighlighter.registerLanguage('csharp',     csharp)
SyntaxHighlighter.registerLanguage('cs',         csharp)
SyntaxHighlighter.registerLanguage('bash',       bash)
SyntaxHighlighter.registerLanguage('sh',         bash)
SyntaxHighlighter.registerLanguage('json',       json)
SyntaxHighlighter.registerLanguage('xml',        xml)
SyntaxHighlighter.registerLanguage('html',       xml)
SyntaxHighlighter.registerLanguage('typescript', ts)
SyntaxHighlighter.registerLanguage('ts',         ts)
SyntaxHighlighter.registerLanguage('javascript', js)
SyntaxHighlighter.registerLanguage('js',         js)
SyntaxHighlighter.registerLanguage('yaml',       yaml)
SyntaxHighlighter.registerLanguage('python',     python)
SyntaxHighlighter.registerLanguage('py',         python)

const ghDark = {
  ...githubGist,
  hljs:                   { ...githubGist.hljs,  background: '#161b22', color: '#e6edf3' },
  'hljs-keyword':         { color: '#ff7b72' },
  'hljs-built_in':        { color: '#ffa657' },
  'hljs-type':            { color: '#ffa657' },
  'hljs-literal':         { color: '#79c0ff' },
  'hljs-number':          { color: '#79c0ff' },
  'hljs-string':          { color: '#a5d6ff' },
  'hljs-comment':         { color: '#8b949e', fontStyle: 'italic' as const },
  'hljs-meta':            { color: '#8b949e' },
  'hljs-attr':            { color: '#79c0ff' },
  'hljs-variable':        { color: '#ffa657' },
  'hljs-title':           { color: '#d2a8ff' },
  'hljs-params':          { color: '#e6edf3' },
  'hljs-addition':        { color: '#aff5b4', background: '#033a16' },
  'hljs-deletion':        { color: '#ffdcd7', background: '#67060c' },
}

function extractLang(className?: string): string | undefined {
  if (!className) return undefined
  const m = className.match(/language-(\w+)/)
  return m ? m[1] : undefined
}

function applyHighlight(text: string, words: string[]): React.ReactNode {
  if (words.length === 0) return text
  const escaped = words.map(w => w.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
  const parts = text.split(new RegExp(`(${escaped.join('|')})`, 'gi'))
  const lower = words.map(w => w.toLowerCase())
  return parts.map((part, i) =>
    lower.includes(part.toLowerCase())
      ? <mark key={i} style={{ background: '#f5c518', color: '#000', borderRadius: '2px', padding: '0 2px' }}>{part}</mark>
      : part
  )
}

interface Props {
  children: string
  className?: string
  highlight?: string
  docPath?: string
  onOpenFile?: (path: string, endpoint?: string) => void
  fileEndpoint?: string
}

export function MarkdownContent({ children, className, highlight, docPath, onOpenFile, fileEndpoint = '/file' }: Props) {
  const hlWords = highlight
    ? highlight.trim().split(/\s+/).filter(w => w.length >= 2)
    : []

  const hl = (text: string) => hlWords.length ? applyHighlight(text, hlWords) : text

  const docDir = docPath
    ? docPath.replace(/\\/g, '/').replace(/\/[^/]+$/, '')
    : null

  function normalizePath(path: string): string {
    const parts: string[] = []
    path.replace(/\\/g, '/').split('/').forEach(part => {
      if (!part || part === '.') { return }
      if (part === '..') {
        const last = parts[parts.length - 1]
        if (parts.length > 0 && last && !last.endsWith(':')) { parts.pop() }
        return
      }
      parts.push(part)
    })
    return parts.join('\\')
  }

  function resolveImageSrc(src: string): string {
    if (!docDir || /^(https?:\/\/|data:)/i.test(src)) return src
    const fullPath = `${docDir}/${src}`.replace(/\//g, '\\')
    return `/image?path=${encodeURIComponent(fullPath)}`
  }

  function resolveFileHref(href: string): string | null {
    if (!href || href.startsWith('#') || /^(https?:\/\/|mailto:|tel:|data:)/i.test(href)) {
      return null
    }
    if (/^\/(file|image|skill-file)\?/i.test(href)) {
      return null
    }

    const pathOnly = href.split('#')[0].split('?')[0]
    if (!pathOnly) { return null }

    let decoded = pathOnly
    try { decoded = decodeURIComponent(pathOnly) } catch { /* keep the original href */ }

    if (/^[A-Za-z]:[\\/]/.test(decoded) || decoded.startsWith('\\\\')) {
      return normalizePath(decoded)
    }
    if (!docDir || decoded.startsWith('/')) {
      return null
    }
    return normalizePath(`${docDir}/${decoded}`)
  }

  return (
    <div className={className}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          a({ href, children: linkChildren, ...props }) {
            const resolved = href ? resolveFileHref(href) : null
            if (resolved && onOpenFile) {
              return (
                <a
                  href={href}
                  {...props}
                  onClick={event => {
                    event.preventDefault()
                    onOpenFile(resolved, fileEndpoint)
                  }}
                >
                  {linkChildren}
                </a>
              )
            }
            if (href && /^(https?:\/\/|mailto:|tel:)/i.test(href)) {
              return <a href={href} target="_blank" rel="noreferrer" {...props}>{linkChildren}</a>
            }
            return <a href={href} {...props}>{linkChildren}</a>
          },
          img({ src, alt }) {
            const resolved = src ? resolveImageSrc(src) : undefined
            return <img src={resolved} alt={alt ?? ''} style={{ maxWidth: '100%' }} />
          },
          code({ className: cls, children: code, ...props }) {
            const lang = extractLang(cls)
            const text = String(code)
            const isBlock = text.includes('\n')
            if (isBlock) {
              return (
                <SyntaxHighlighter
                  style={ghDark}
                  language={lang ?? 'plaintext'}
                  PreTag="div"
                  customStyle={{ margin: '0.25rem 0', borderRadius: '0.375rem', fontSize: '0.75rem' }}
                >
                  {text.replace(/\n$/, '')}
                </SyntaxHighlighter>
              )
            }
            return <code className="bg-gh-card text-gh-accent px-1 rounded text-xs font-mono" {...props}>{code}</code>
          },
          p({ children: c })   { return <p>{applyToChildren(c, hl)}</p> },
          li({ children: c })  { return <li>{applyToChildren(c, hl)}</li> },
          td({ children: c })  { return <td>{applyToChildren(c, hl)}</td> },
          th({ children: c })  { return <th>{applyToChildren(c, hl)}</th> },
        }}
      >
        {children}
      </ReactMarkdown>
    </div>
  )
}

function applyToChildren(
  children: React.ReactNode,
  hl: (t: string) => React.ReactNode
): React.ReactNode {
  return React.Children.map(children, child =>
    typeof child === 'string' ? hl(child) : child
  )
}
