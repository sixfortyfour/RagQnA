<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <div class="min-h-screen bg-[#020c1b] flex flex-col">
    <nav class="bg-white border-b border-gray-200 shadow-sm">
      <div class="max-w-3xl mx-auto px-4 sm:px-6 flex items-center justify-between h-14">
        <span class="text-base font-bold text-gray-900 tracking-tight">Knowably</span>
        <div class="flex gap-1">
          <RouterLink
            to="/upload"
            class="rounded-lg px-3 py-1.5 text-sm font-medium text-gray-600 hover:bg-gray-100 transition-colors"
            active-class="bg-blue-50 text-blue-700"
          >
            Upload
          </RouterLink>
          <RouterLink
            to="/ask"
            class="rounded-lg px-3 py-1.5 text-sm font-medium text-gray-600 hover:bg-gray-100 transition-colors"
            active-class="bg-blue-50 text-blue-700"
          >
            Ask
          </RouterLink>
          <RouterLink
            to="/about"
            class="rounded-lg px-3 py-1.5 text-sm font-medium text-gray-600 hover:bg-gray-100 transition-colors"
            active-class="bg-blue-50 text-blue-700"
          >
            About
          </RouterLink>
        </div>
      </div>
    </nav>
    <main class="flex-1 max-w-3xl w-full mx-auto px-4 sm:px-6 py-10">
      <RouterView />
    </main>
    <footer class="py-4 text-center text-xs text-slate-600">
      © 2026 sixfortyfour
    </footer>
  </div>
</template>
