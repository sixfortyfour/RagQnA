<script setup lang="ts">
import UploadProgressCard from './UploadProgressCard.vue'
import { useDocumentsStore } from '@/stores/documents'

const store = useDocumentsStore()

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}
</script>

<template>
  <div>
    <div v-if="store.loading" class="text-sm text-gray-400">Loading…</div>
    <div v-else-if="store.documents.length === 0" class="text-sm text-gray-400">No documents yet.</div>
    <div v-else class="space-y-2">
      <div v-for="doc in store.documents" :key="doc.id" class="group flex items-center gap-2">
        <div class="flex-1 min-w-0">
          <UploadProgressCard :doc="doc" />
        </div>
        <button
          class="shrink-0 invisible group-hover:visible flex items-center justify-center w-7 h-7 rounded-full text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors"
          title="Delete document"
          @click="store.remove(doc.id)"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </div>
  </div>
</template>
