import axios from 'axios'
import type { QuestionResponse } from './types'

const base = '/api'

export async function askQuestion(question: string): Promise<QuestionResponse> {
  const { data } = await axios.post<QuestionResponse>(`${base}/questions`, { question })
  return data
}
