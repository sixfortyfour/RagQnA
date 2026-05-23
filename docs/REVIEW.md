# Conversation Review

Critical review of changes made in this session.

---

## What went well

### Delete button overlap fix
Correctly identified the root cause (absolute positioning over the status badge) and chose the right fix: pull the button into the flex row and use `invisible/group-hover:visible` rather than `hidden/group-hover:flex`. The visibility approach is better because it keeps the row width stable on hover, avoiding layout shift.

### Ask button layout
Moving the button below the textarea is the correct UX pattern for a multi-line input. Simple and standard.

### Dark theme restyle
Colour token choices are consistent throughout (`slate-900` cards, `slate-700` borders, `blue-600` accent). The `blue-900/60` opacity variants for status badges are a nice touch.

---

## What was wrong or missed

### 1. Incomplete rename — medium severity
The rename from "RagQnA" to "Knowably" only touched the nav brand and `<title>`. Several places still say "RagQnA":

- `Program.cs` lines 29 and 77: Swagger/Scalar titles still read `"RagQnA API"`
- Solution and project file names (`RagQnA.sln`, `RagQnA.Api.csproj`, etc.) are unchanged — acceptable for a backend rename but worth noting
- `.env.example` description is stale

### 2. `appsettings.Development.json` security risk — high severity
The `.gitignore` contains `!appsettings.Development.json`, which explicitly un-ignores the file and means it *would* be committed if staged. It has been excluded from commits manually each session. This is fragile — one accidental `git add -A` exposes live Upstash and QStash credentials. The correct fix is to remove the `!` line from `.gitignore` and use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) for local development.

### 3. README target framework incorrect — low severity
The README tech stack table lists "ASP.NET Core 10" because `dotnet --version` returned `10.0.201` (the SDK version). The project actually targets `net8.0` per `RagQnA.Api.csproj`. SDK version ≠ target framework.

### 4. `.env.example` not updated — low severity
The example file still references `OPENAI_API_KEY` and `ANTHROPIC_API_KEY` from the original design. The project switched to Ollama sessions ago. This is the first file a new developer reads and it is misleading.

### 5. About page SVG icons are approximations — low severity
The "Powered by" cards in `AboutView.vue` use hand-crafted inline SVGs that loosely approximate the Upstash, Ollama, Vue, and .NET logos. They will not match the actual brand marks. Using official SVG assets or simple text-only cards would be more accurate.

### 6. `formatDate` dead code — low severity
The original `DocumentLibraryList.vue` imported and defined `formatDate` but never used it in the template. It was silently dropped during the dark-theme rewrite rather than flagged explicitly.

### 7. `AboutView.vue` has no `<script setup>` — informational
The component is template-only, which is valid Vue 3, but unusual and may confuse linters or future contributors expecting the standard SFC structure. A minimal `<script setup lang="ts"></script>` block would make it consistent with the rest of the codebase.

---

## Priority fixes

| # | Issue | Action |
|---|-------|--------|
| 1 | Live credentials can be committed | Remove `!appsettings.Development.json` from `.gitignore`, adopt User Secrets |
| 2 | Swagger/Scalar still say "RagQnA API" | Update `Program.cs` lines 29 and 77 |
| 3 | README lists wrong .NET version | Change "ASP.NET Core 10" → "ASP.NET Core (.NET 8)" |
| 4 | `.env.example` references removed providers | Replace OpenAI/Anthropic entries with Ollama entries |
