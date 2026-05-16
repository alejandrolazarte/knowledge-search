export type SourceKind = 'Knowledge' | 'Repository'

export interface SourceDefinition {
  id:           string
  name:         string
  kind:         SourceKind
  hostPath:     string
  indexCode:    boolean
  indexDocs:    boolean
  docIncludes:  string[]
  codeIncludes: string[]
  excludes:     string[]
}

export interface SourceConfigurationFile {
  version: number
  sources: SourceDefinition[]
}

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
  filePath:    string
}

export interface LogEvent {
  ts:   string
  type: 'added' | 'updated' | 'deleted' | 'error'
  path: string
}

export interface CrossRepoSearchNode {
  repositoryName: string
  identifier:     string
  name:           string
  kind:           string
  filePath:       string
  line:           number
  weight:         number
}

export interface CrossRepoSearchEdge {
  repositoryName:   string
  sourceIdentifier: string
  targetIdentifier: string
  kind:             string
  line:             number
}

export interface CrossRepoLink {
  sourceRepositoryName: string
  sourceIdentifier:     string
  targetRepositoryName: string
  targetIdentifier:     string
  kind:                 string
}

export interface CrossRepoSubgraphResponse {
  nodes:          CrossRepoSearchNode[]
  edges:          CrossRepoSearchEdge[]
  crossRepoLinks: CrossRepoLink[]
  query:          string
  depth:          number
  totalFound:     number
}

export interface CodeDocumentSearchResult {
  repositoryName: string
  identifier:     string
  name:           string
  kind:           string
  filePath:       string
  line:           number
  content:        string
  score:          number
}
