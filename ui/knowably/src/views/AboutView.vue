<template>
  <div class="max-w-2xl space-y-10">
    <div class="space-y-4">
      <h1 class="text-2xl font-bold text-white">About the project</h1>
      <p class="text-slate-300 leading-relaxed">
        A full-stack portfolio project demonstrating
        <span class="text-blue-400">Retrieval-Augmented Generation (RAG)</span>
        using an
        <a href="https://dotnet.microsoft.com/apps/aspnet" target="_blank" rel="noopener" class="text-blue-400 hover:underline">ASP.NET Core</a>
        Web API and a
        <a href="https://vuejs.org" target="_blank" rel="noopener" class="text-blue-400 hover:underline">Vue.js</a>
        +
        <a href="https://www.typescriptlang.org" target="_blank" rel="noopener" class="text-blue-400 hover:underline">TypeScript</a>
        frontend styled with
        <a href="https://tailwindcss.com" target="_blank" rel="noopener" class="text-blue-400 hover:underline">Tailwind CSS</a>.
        Documents are chunked, embedded, and stored in a vector index for semantic search.
        Questions are answered by a local LLM grounded in the retrieved context, with results
        cached to avoid redundant inference calls.
        Built with help from
        <a href="https://claude.ai/code" target="_blank" rel="noopener" class="text-blue-400 hover:underline">Claude Code</a>.
        The project is fully open source and available on
        <a href="https://github.com/sixfortyfour/Knowably" target="_blank" rel="noopener" class="text-blue-400 hover:underline">GitHub</a>.
      </p>
    </div>

    <div class="space-y-4">
      <p class="text-sm font-semibold uppercase tracking-wide text-slate-500">Powered by</p>
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <a
          href="https://upstash.com"
          target="_blank" rel="noopener"
          class="flex items-center gap-3 rounded-xl border border-slate-700 bg-slate-900 px-5 py-4 hover:border-slate-500 transition-colors"
        >
          <svg class="w-7 h-7 shrink-0" viewBox="0 0 40 40" fill="none">
            <path d="M20 5C11.716 5 5 11.716 5 20s6.716 15 15 15 15-6.716 15-15S28.284 5 20 5z" fill="#00E9A3" opacity=".15"/>
            <path d="M20 10c-5.523 0-10 4.477-10 10s4.477 10 10 10 10-4.477 10-10S25.523 10 20 10zm0 16a6 6 0 110-12 6 6 0 010 12z" fill="#00E9A3"/>
          </svg>
          <div>
            <p class="text-sm font-semibold text-white">Upstash</p>
            <p class="text-xs text-slate-400">Redis · Vector · QStash</p>
          </div>
        </a>

        <a
          href="https://ollama.com"
          target="_blank" rel="noopener"
          class="flex items-center gap-3 rounded-xl border border-slate-700 bg-slate-900 px-5 py-4 hover:border-slate-500 transition-colors"
        >
          <svg class="w-7 h-7 shrink-0 text-white" viewBox="0 0 40 40" fill="none">
            <circle cx="20" cy="20" r="15" stroke="currentColor" stroke-width="2" opacity=".3"/>
            <circle cx="20" cy="20" r="8" fill="currentColor" opacity=".8"/>
            <circle cx="16" cy="17" r="2" fill="#020c1b"/>
            <circle cx="24" cy="17" r="2" fill="#020c1b"/>
          </svg>
          <div>
            <p class="text-sm font-semibold text-white">Ollama</p>
            <p class="text-xs text-slate-400">nomic-embed-text · llama3</p>
          </div>
        </a>

        <a
          href="https://dotnet.microsoft.com/apps/aspnet"
          target="_blank" rel="noopener"
          class="flex items-center gap-3 rounded-xl border border-slate-700 bg-slate-900 px-5 py-4 hover:border-slate-500 transition-colors"
        >
          <svg class="w-7 h-7 shrink-0" viewBox="0 0 40 40" fill="none">
            <path d="M20 6L34 14v12L20 34 6 26V14L20 6z" fill="#512BD4" opacity=".2"/>
            <path d="M20 6L34 14v12L20 34 6 26V14L20 6z" stroke="#512BD4" stroke-width="1.5"/>
            <text x="20" y="24" text-anchor="middle" font-size="10" font-weight="bold" fill="#512BD4">.NET</text>
          </svg>
          <div>
            <p class="text-sm font-semibold text-white">ASP.NET Core</p>
            <p class="text-xs text-slate-400">Web API · .NET 8</p>
          </div>
        </a>

        <a
          href="https://vuejs.org"
          target="_blank" rel="noopener"
          class="flex items-center gap-3 rounded-xl border border-slate-700 bg-slate-900 px-5 py-4 hover:border-slate-500 transition-colors"
        >
          <svg class="w-7 h-7 shrink-0" viewBox="0 0 40 40" fill="none">
            <path d="M20 8l8 14H12L20 8z" fill="#42b883"/>
            <path d="M20 8L28 22H12L20 8z" fill="#42b883" opacity=".4"/>
            <path d="M12 22l8-14 8 14" stroke="#35495e" stroke-width="0" fill="none"/>
            <path d="M4 8l16 26L36 8h-6L20 28 10 8H4z" fill="#35495e" opacity=".15"/>
            <path d="M10 8l10 18L30 8h-4l-6 12-6-12h-4z" fill="#42b883" opacity=".7"/>
          </svg>
          <div>
            <p class="text-sm font-semibold text-white">Vue 3</p>
            <p class="text-xs text-slate-400">TypeScript · Vite · Pinia</p>
          </div>
        </a>
      </div>
    </div>
  </div>
</template>
