import { defineStore } from 'pinia'
import { ref } from 'vue'
import { askQuestion } from '@/api/questions'
import type { QuestionResponse } from '@/api/types'

const HISTORY_KEY = 'rag:recent-questions'
const MAX_HISTORY = 5

export const useQuestionsStore = defineStore('questions', () => {
  const current = ref<QuestionResponse | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const history = ref<QuestionResponse[]>(loadHistory())

  function loadHistory(): QuestionResponse[] {
    try {
      return JSON.parse(localStorage.getItem(HISTORY_KEY) ?? '[]')
    } catch {
      return []
    }
  }

  function saveHistory() {
    localStorage.setItem(HISTORY_KEY, JSON.stringify(history.value))
  }

  async function ask(question: string) {
    loading.value = true
    error.value = null
    current.value = null
    try {
      const response = await askQuestion(question)
      current.value = response
      history.value = [response, ...history.value.filter((h) => h.question !== response.question)].slice(
        0,
        MAX_HISTORY,
      )
      saveHistory()
    } catch (e) {
      error.value = 'Failed to get an answer. Please try again.'
    } finally {
      loading.value = false
    }
  }

  function remove(question: string) {
    history.value = history.value.filter((h) => h.question !== question)
    saveHistory()
  }

  return { current, loading, error, history, ask, remove }
})
