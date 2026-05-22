<script setup lang="ts">
import QuestionInput from '@/components/QuestionInput.vue'
import AnswerCard from '@/components/AnswerCard.vue'
import SourceChunksPanel from '@/components/SourceChunksPanel.vue'
import RecentQuestionsPanel from '@/components/RecentQuestionsPanel.vue'
import { useQuestionsStore } from '@/stores/questions'

const store = useQuestionsStore()

function onAsk(question: string) {
  store.ask(question)
}
</script>

<template>
  <div class="space-y-8">
    <div>
      <h1 class="text-2xl font-bold text-white">Ask a Question</h1>
      <p class="mt-1 text-sm text-slate-400">Get answers grounded in your uploaded documents.</p>
    </div>

    <div class="space-y-4">
      <QuestionInput :loading="store.loading" @ask="onAsk" />
      <p v-if="store.error" class="text-sm text-red-400">{{ store.error }}</p>
    </div>

    <template v-if="store.current">
      <AnswerCard :response="store.current" />
      <SourceChunksPanel
        v-if="store.current.sourceChunks.length > 0"
        :chunks="store.current.sourceChunks"
      />
    </template>

    <div v-if="store.loading" class="flex items-center gap-2 text-sm text-slate-400">
      <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8z" />
      </svg>
      Searching and generating answer…
    </div>

    <RecentQuestionsPanel :history="store.history" @reask="onAsk" @remove="store.remove" />
  </div>
</template>
