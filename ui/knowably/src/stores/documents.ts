import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  uploadDocument,
  listDocuments,
  getDocumentStatus,
  deleteDocument,
} from '@/api/documents'
import type { DocumentListItem, DocumentMetadata } from '@/api/types'

export const useDocumentsStore = defineStore('documents', () => {
  const documents = ref<DocumentListItem[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const pollingIntervals = new Map<string, ReturnType<typeof setInterval>>()

  async function fetchDocuments() {
    loading.value = true
    error.value = null
    try {
      documents.value = await listDocuments()
    } catch (e) {
      error.value = 'Failed to load documents.'
    } finally {
      loading.value = false
    }
  }

  async function upload(file: File): Promise<string> {
    const result = await uploadDocument(file)
    const pending: DocumentListItem = {
      id: result.documentId,
      fileName: file.name,
      status: 'Pending',
      createdAt: new Date().toISOString(),
      chunkCount: null,
    }
    documents.value = [pending, ...documents.value]
    startPolling(result.documentId)
    return result.documentId
  }

  function startPolling(id: string) {
    const interval = setInterval(async () => {
      try {
        const meta: DocumentMetadata = await getDocumentStatus(id)
        const idx = documents.value.findIndex((d) => d.id === id)
        if (idx !== -1) {
          documents.value[idx] = { ...documents.value[idx], ...meta }
        }
        if (meta.status === 'Indexed' || meta.status === 'Failed') {
          stopPolling(id)
        }
      } catch {
        stopPolling(id)
      }
    }, 2000)
    pollingIntervals.set(id, interval)
  }

  function stopPolling(id: string) {
    const interval = pollingIntervals.get(id)
    if (interval) {
      clearInterval(interval)
      pollingIntervals.delete(id)
    }
  }

  async function remove(id: string) {
    stopPolling(id)
    await deleteDocument(id)
    documents.value = documents.value.filter((d) => d.id !== id)
  }

  return { documents, loading, error, fetchDocuments, upload, remove }
})
