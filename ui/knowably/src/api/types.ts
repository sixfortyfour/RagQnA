export type DocumentStatus = 'Pending' | 'Indexing' | 'Indexed' | 'Failed'

export interface DocumentListItem {
  id: string
  fileName: string
  status: DocumentStatus
  createdAt: string
  chunkCount: number | null
}

export interface DocumentMetadata extends DocumentListItem {
  indexedAt: string | null
  errorMessage: string | null
}

export interface UploadDocumentResponse {
  documentId: string
  statusUrl: string
}

export interface SourceChunk {
  text: string
  documentId: string
  chunkIndex: number
  score: number
}

export interface QuestionResponse {
  question: string
  answer: string
  cached: boolean
  durationMs: number
  sourceChunks: SourceChunk[]
}
