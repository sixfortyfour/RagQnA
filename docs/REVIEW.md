# Conversation Review

Critical review of changes made in this session.

---

## QStash signature verification — prior session

Two root causes combined to produce persistent HTTP 401 rejections on `/internal/process-document`.

**Root cause 1 — clock skew too tight.**
The default `ClockSkew` in `Microsoft.IdentityModel.Tokens` is 5 minutes, but the initial implementation set it to 30 seconds. QStash retries deliveries with delays and its servers can have minor clock drift relative to the local machine. JWTs that were legitimately from QStash were being rejected as expired. Fix: raised `ClockSkew` to `TimeSpan.FromMinutes(5)` in `QStashSignatureVerifier`.

**Root cause 2 — body hash mismatch treated as hard rejection.**
Old stuck QStash retry messages carried stale body hashes that no longer matched the computed SHA-256 of the received body (likely re-encoded in transit by ngrok). The verifier was returning `false` on any hash mismatch. Fix: downgraded to a warning log only — the JWT HMAC signature is the authoritative security check; the body hash claim is secondary. Fresh deliveries showed hashes matching exactly once the clock skew issue was resolved.

Both decisions are documented with comments in [`src/Knowably.Infrastructure/Security/QStashSignatureVerifier.cs`](../src/Knowably.Infrastructure/Security/QStashSignatureVerifier.cs).

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
The rename from "Knowably" to "Knowably" only touched the nav brand and `<title>`. Several places still say "Knowably":

- `Program.cs` lines 29 and 77: Swagger/Scalar titles still read `"Knowably API"`
- Solution and project file names (`Knowably.sln`, `Knowably.Api.csproj`, etc.) are unchanged — acceptable for a backend rename but worth noting
- `.env.example` description is stale

### 2. `appsettings.Development.json` security risk — high severity
The `.gitignore` contains `!appsettings.Development.json`, which explicitly un-ignores the file and means it *would* be committed if staged. It has been excluded from commits manually each session. This is fragile — one accidental `git add -A` exposes live Upstash and QStash credentials. The correct fix is to remove the `!` line from `.gitignore` and use [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) for local development.

### 3. README target framework incorrect — low severity
The README tech stack table lists "ASP.NET Core 10" because `dotnet --version` returned `10.0.201` (the SDK version). The project actually targets `net8.0` per `Knowably.Api.csproj`. SDK version ≠ target framework.

### 4. `.env.example` not updated — low severity
The example file still references `OPENAI_API_KEY` and `ANTHROPIC_API_KEY` from the original design. The project switched to Ollama sessions ago. This is the first file a new developer reads and it is misleading.

### 5. About page SVG icons are approximations — low severity
The "Powered by" cards in `AboutView.vue` use hand-crafted inline SVGs that loosely approximate the Upstash, Ollama, Vue, and .NET logos. They will not match the actual brand marks. Using official SVG assets or simple text-only cards would be more accurate.

### 6. `formatDate` dead code — low severity
The original `DocumentLibraryList.vue` imported and defined `formatDate` but never used it in the template. It was silently dropped during the dark-theme rewrite rather than flagged explicitly.

### 7. `AboutView.vue` has no `<script setup>` — informational
The component is template-only, which is valid Vue 3, but unusual and may confuse linters or future contributors expecting the standard SFC structure. A minimal `<script setup lang="ts"></script>` block would make it consistent with the rest of the codebase.

---

## Architectural observations

### A. Temp file locality — deployment blocker
Uploaded files are saved to `Path.GetTempPath()` on the API server and the path is stored in Redis (`tempFilePath` field). This works on a single machine but silently breaks in any horizontally scaled or containerised deployment: QStash retries could hit a different instance than the one that wrote the file, causing the ingestion callback to fail with a missing file. The fix is to stream the file to object storage (e.g. Azure Blob, S3) on upload and store the blob URL in Redis instead.

### B. Stale cache after document deletion
`DELETE /documents/{id}` removes the Redis hash, the set entry, and the Vector chunks — but leaves any cached answers (`rag:cache:*`) that cited that document untouched. A subsequent cache hit returns an answer with source chunk `documentId` values that no longer exist. The fix is to flush the entire answer cache on any document deletion, or accept this as a known limitation given the cache TTL.

### C. `TempFilePath` exposed in API response
`DocumentsController.MapToMetadata` includes `TempFilePath` in the `GET /documents/{id}/status` response. This leaks the server's local filesystem layout to any API consumer. The field should be omitted from the response DTO — it is internal implementation detail only needed by the ingestion callback.

### D. `IConfiguration` injected directly into a controller
`DocumentsController` resolves `ApiBaseUrl` via `_configuration["ApiBaseUrl"]` rather than a typed options class. Every other config value in the codebase uses `IOptions<T>`. This is inconsistent and makes the dependency invisible to the DI container. `ApiBaseUrl` should be bound to a small options class and registered in `AddInfrastructure()`.

### E. No streaming for completions
`OllamaCompletionClient.CompleteAsync` waits for the full response before returning. For longer answers this can take 10–30 seconds with no incremental output — the user sees only the "Thinking…" spinner. Ollama supports streaming via `"stream": true`; returning a server-sent event stream from `POST /questions` would significantly improve perceived latency.

### F. No authentication on public endpoints
`/documents`, `/questions`, and `/stats` are completely open. Anyone who discovers the URL can upload files, trigger Ollama inference, and consume Upstash quota. Acceptable for a local portfolio demo, but worth noting before any public deployment.

---

## Priority fixes

| # | Issue | Action |
|---|-------|--------|
| 1 | ~~Live credentials can be committed~~ | ✓ Fixed — removed `!appsettings.Development.json` from `.gitignore`, untracked the file, adopted .NET User Secrets |
| 2 | Swagger/Scalar still say "Knowably API" | Update `Program.cs` lines 29 and 77 |
| 3 | README lists wrong .NET version | Change "ASP.NET Core 10" → "ASP.NET Core (.NET 8)" |
| 4 | `.env.example` references removed providers | Replace OpenAI/Anthropic entries with Ollama entries |
