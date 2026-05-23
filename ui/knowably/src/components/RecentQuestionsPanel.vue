<script setup lang="ts">
import type { QuestionResponse } from '@/api/types'

defineProps<{ history: QuestionResponse[] }>()
const emit = defineEmits<{
  (e: 'reask', question: string): void
  (e: 'remove', question: string): void
}>()
</script>

<template>
  <div v-if="history.length > 0" class="space-y-2">
    <h3 class="text-xs font-semibold uppercase tracking-wide text-slate-500">Recent questions</h3>
    <div v-for="item in history" :key="item.question" class="group flex items-center gap-2">
      <button
        class="flex-1 min-w-0 text-left rounded-xl border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-300 hover:border-blue-500/50 hover:bg-slate-800 transition-colors"
        @click="emit('reask', item.question)"
      >
        <span class="line-clamp-1">{{ item.question }}</span>
      </button>
      <button
        class="shrink-0 invisible group-hover:visible flex items-center justify-center w-7 h-7 rounded-full text-slate-500 hover:text-red-400 hover:bg-red-900/30 transition-colors"
        title="Remove from history"
        @click="emit('remove', item.question)"
      >
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </div>
  </div>
</template>
