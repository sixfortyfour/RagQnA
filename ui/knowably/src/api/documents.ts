import axios from 'axios'
import type { DocumentListItem, DocumentMetadata, UploadDocumentResponse } from './types'

const base = '/api'

export async function uploadDocument(file: File): Promise<UploadDocumentResponse> {
  const form = new FormData()
  form.append('file', file)
  const { data } = await axios.post<UploadDocumentResponse>(`${base}/documents`, form)
  return data
}

export async function listDocuments(): Promise<DocumentListItem[]> {
  const { data } = await axios.get<DocumentListItem[]>(`${base}/documents`)
  return data
}

export async function getDocumentStatus(id: string): Promise<DocumentMetadata> {
  const { data } = await axios.get<DocumentMetadata>(`${base}/documents/${id}/status`)
  return data
}

export async function deleteDocument(id: string): Promise<void> {
  await axios.delete(`${base}/documents/${id}`)
}
