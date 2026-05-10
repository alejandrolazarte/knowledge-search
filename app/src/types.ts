export interface SearchResult {
  title:   string
  section: string
  path:    string
  line:    number
  content: string
  root:    string
}

export interface Skill {
  name:        string
  description: string
  dirName:     string
}

export interface LogEvent {
  ts:   string
  type: 'added' | 'updated' | 'deleted' | 'error'
  path: string
}
