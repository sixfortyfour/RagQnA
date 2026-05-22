<script setup lang="ts">
import type { QuestionResponse } from '@/api/types'

defineProps<{ response: QuestionResponse }>()
</script>

<template>
  <div class="rounded-xl border border-slate-700 bg-slate-900 p-5 shadow-sm space-y-3">
    <div class="flex items-start justify-between gap-2">
      <p class="text-sm font-medium text-slate-400 italic">"{{ response.question }}"</p>
      <div class="flex items-center gap-2 shrink-0">
        <span
          v-if="response.cached"
          class="inline-flex items-center rounded-full bg-blue-900/60 px-2.5 py-0.5 text-xs font-medium text-blue-300"
        >
          From cache
        </span>
        <span class="text-xs text-slate-500">{{ response.durationMs }}ms</span>
      </div>
    </div>
    <p class="text-sm text-slate-100 leading-relaxed whitespace-pre-wrap">{{ response.answer }}</p>
  </div>
</template>
