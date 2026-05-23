<script setup lang="ts">
import { ref } from 'vue'

const emit = defineEmits<{ (e: 'file-selected', file: File): void }>()

const isDragging = ref(false)
const errorMsg = ref<string | null>(null)
const ALLOWED = ['.pdf', '.txt', '.md']

function validate(file: File): string | null {
  const ext = '.' + file.name.split('.').pop()?.toLowerCase()
  if (!ALLOWED.includes(ext)) return `Unsupported type "${ext}". Allowed: ${ALLOWED.join(', ')}`
  if (file.size > 5 * 1024 * 1024) return 'File exceeds 5 MB limit.'
  return null
}

function handleFile(file: File) {
  errorMsg.value = validate(file)
  if (!errorMsg.value) emit('file-selected', file)
}

function onDrop(e: DragEvent) {
  isDragging.value = false
  const file = e.dataTransfer?.files[0]
  if (file) handleFile(file)
}

function onInput(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (file) handleFile(file)
}
</script>

<template>
  <label
    class="flex flex-col items-center justify-center w-full h-40 border-2 border-dashed rounded-xl cursor-pointer transition-colors"
    :class="isDragging
      ? 'border-blue-500 bg-blue-900/20'
      : 'border-slate-700 bg-slate-900 hover:border-slate-500 hover:bg-slate-800'"
    @dragover.prevent="isDragging = true"
    @dragleave="isDragging = false"
    @drop.prevent="onDrop"
  >
    <div class="flex flex-col items-center gap-2 text-slate-400">
      <svg class="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
          d="M12 16v-8m0 0-3 3m3-3 3 3M6 20h12a2 2 0 002-2V8l-6-6H6a2 2 0 00-2 2v14a2 2 0 002 2z" />
      </svg>
      <span class="text-sm font-medium text-slate-300">Drop a file here or <span class="text-blue-400">browse</span></span>
      <span class="text-xs text-slate-500">PDF, TXT, MD — max 5 MB</span>
    </div>
    <input type="file" class="hidden" accept=".pdf,.txt,.md" @change="onInput" />
  </label>
  <p v-if="errorMsg" class="mt-2 text-sm text-red-400">{{ errorMsg }}</p>
</template>
