import { createRouter, createWebHistory } from 'vue-router'
import DocumentUploadView from '@/views/DocumentUploadView.vue'
import QuestionAnswerView from '@/views/QuestionAnswerView.vue'
import AboutView from '@/views/AboutView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', redirect: '/upload' },
    { path: '/upload', component: DocumentUploadView },
    { path: '/ask', component: QuestionAnswerView },
    { path: '/about', component: AboutView },
  ],
})

export default router
