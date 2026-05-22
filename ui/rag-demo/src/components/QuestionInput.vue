<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{ loading: boolean }>()
const emit = defineEmits<{ (e: 'ask', question: string): void }>()

const question = ref('')

function submit() {
  const q = question.value.trim()
  if (!q || props.loading) return
  emit('ask', q)
  question.value = ''
}
</script>

<template>
  <div class="space-y-3">
    <textarea
      v-model="question"
      rows="4"
      placeholder="Ask a question about your documents…"
      class="w-full resize-none rounded-xl bg-slate-900 border border-slate-700 px-4 py-3 text-sm text-white placeholder:text-slate-500 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-50"
      :disabled="loading"
      @keydown.enter.meta.prevent="submit"
      @keydown.enter.ctrl.prevent="submit"
    />
    <div class="flex justify-end">
      <button
        class="rounded-full bg-blue-600 px-6 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        :disabled="loading || !question.trim()"
        @click="submit"
      >
        <span v-if="loading">Thinking…</span>
        <span v-else>Ask</span>
      </button>
    </div>
  </div>
</template>
