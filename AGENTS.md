# RAG-Powered Document Q&A — Project Plan

A full-stack portfolio project demonstrating Retrieval-Augmented Generation (RAG) using
all three Upstash products (Redis, Vector, QStash), an ASP.NET Core Web API, and two
Vue 3 + TypeScript frontends.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Solution Structure](#2-solution-structure)
3. [Upstash Setup](#3-upstash-setup)
4. [Phase 1 — Infrastructure Layer (C#)](#4-phase-1--infrastructure-layer-c)
5. [Phase 2 — Core API](#5-phase-2--core-api)
6. [Phase 3 — Ingestion Pipeline](#6-phase-3--ingestion-pipeline)
7. [Phase 4 — Query Pipeline](#7-phase-4--query-pipeline)
8. [Phase 5 — Monitor API Endpoints](#8-phase-5--monitor-api-endpoints)
9. [Phase 6 — Demo UI (rag-demo)](#9-phase-6--demo-ui-rag-demo)
10. [Phase 7 — Monitor UI (rag-monitor)](#10-phase-7--monitor-ui-rag-monitor)
11. [Phase 8 — Testing](#11-phase-8--testing)
12. [Phase 9 — Deployment & Polish](#12-phase-9--deployment--polish)
13. [Environment Variables Reference](#13-environment-variables-reference)
14. [NuGet & npm Dependencies](#14-nuget--npm-dependencies)
15. [API Endpoint Reference](#15-api-endpoint-reference)

---

## 1. Architecture Overview

```
[Demo UI]          [Monitor UI]
knowably/          knowably-monitor/
(Vue 3 + TS)       (Vue 3 + TS)
     |                   |
     └─────────┬─────────┘
               ▼
       Knowably.Api  (ASP.NET Core)
       ┌────────────────────────┐
       │  /documents            │
       │  /questions            │
       │  /stats                │
       │  /monitor/*            │
       │  /internal/*  ◄── QStash callbacks
       └────────┬───────────────┘
                │
    ┌───────────┼───────────────┐
    ▼           ▼               ▼
Upstash      Upstash         Upstash
 Redis        Vector          QStash
(cache +     (embeddings)    (ingestion
 status +                     jobs)
 counters)
                ▲
           Ollama
         (embeddings +
          completions,
          runs locally)
```

### Data Flow Summary

**Ingestion (async)**
```
POST /documents → 202 Accepted
  → Redis: store doc metadata (status: pending)
  → QStash: publish job to /internal/process-document
  → QStash calls back → chunk → embed → upsert to Vector
  → Redis: update status to indexed
```

**Query (synchronous, cached)**
```
POST /questions
  → hash question → Redis GET (cache check)
  → HIT:  return cached answer immediately
  → MISS: embed question → Vector query (top_k=5)
          → build prompt → Anthropic completion
          → Redis SET with TTL → return answer + source chunks
```

---

## 2. Solution Structure

```
RagQnA/
├── Knowably.slnx
├── src/
│   ├── Knowably.Api/                  # ASP.NET Core host, controllers, middleware
│   ├── Knowably.Ingestion/            # Chunking, embedding orchestration
│   ├── Knowably.Infrastructure/       # Typed HTTP clients for all three Upstash products
│   └── Knowably.Contracts/            # DTOs, interfaces, enums (no dependencies)
├── tests/
│   └── Knowably.Tests/                # xUnit — unit tests for pure logic (no Upstash required)
├── ui/
│   ├── knowably/                    # Vue 3 + TS — public demo interface
│   └── knowably-monitor/                 # Vue 3 + TS — internal monitoring dashboard
├── .env.example
├── .gitignore
└── README.md
```

---

## 3. Upstash Setup

### Steps
- [ ] Create a free account at https://console.upstash.com
- [ ] Create a **Redis** database → copy `UPSTASH_REDIS_REST_URL` and `UPSTASH_REDIS_REST_TOKEN`
- [ ] Create a **Vector** index
  - Dimensions: `768` (matches Ollama `nomic-embed-text`)
  - Metric: Cosine
  - Copy `UPSTASH_VECTOR_REST_URL` and `UPSTASH_VECTOR_REST_TOKEN`
- [ ] Copy `QSTASH_TOKEN` from the QStash section
- [ ] Copy `QSTASH_CURRENT_SIGNING_KEY` and `QSTASH_NEXT_SIGNING_KEY` for signature verification

> **AI provider note:** This project uses **Ollama** (runs locally, free) for both embeddings
> and completions. Embedding model: `nomic-embed-text` (768 dimensions). Completion model:
> `llama3.2`. To switch providers, implement `IEmbeddingClient` / `ICompletionClient` and
> update the DI registrations in `InfrastructureServiceExtensions`. If you change the embedding
> model, recreate the Vector index with the matching dimension count.

### Redis Key Schema

| Key pattern                        | Type   | Purpose                            |
|------------------------------------|--------|------------------------------------|
| `rag:doc:{id}`                     | Hash   | Document metadata + status         |
| `rag:doc:all`                      | Set    | All document IDs                   |
| `rag:cache:{questionHash}`         | String | Cached Q&A answer (with TTL)       |
| `rag:stats:queries`                | String | INCR counter — total queries       |
| `rag:stats:hits`                   | String | INCR counter — cache hits          |
| `rag:stats:misses`                 | String | INCR counter — cache misses        |
| `rag:stats:history:{bucket}`       | Hash   | Per-minute hit/miss bucket         |

> **Cache key tracking:** Do not maintain a separate Set of cache keys — entries removed by TTL
> expiry will never be cleaned from the Set, causing drift. Instead, use `KeysAsync("rag:cache:*")`
> in the monitor endpoint. This is a SCAN-based operation and acceptable at portfolio scale.

> **Document deletion cascade:** Deleting a document requires three steps: (1) `DEL rag:doc:{id}`,
> (2) `SREM rag:doc:all {id}`, (3) delete all Vector chunks where metadata `docId == {id}` using
> `UpstashVectorClient.DeleteByFilterAsync()`.

---

## 4. Phase 1 — Infrastructure Layer (C#)

**Project:** `Knowably.Infrastructure`

All three Upstash products expose a REST API — no official C# SDK exists, so you'll build
clean typed clients over `HttpClient`. This is a portfolio strength.

### 4.1 UpstashRedisClient

- [x] Create `IUpstashRedisClient` interface in `Knowably.Contracts`
- [x] Implement `UpstashRedisClient : IUpstashRedisClient`
  - [x] `GetAsync(string key) → Task<string?>`
  - [x] `SetAsync(string key, string value, TimeSpan? ttl) → Task`
  - [x] `IncrAsync(string key) → Task<long>`
  - [x] `HSetAsync(string key, Dictionary<string, string> fields) → Task`
  - [x] `HGetAllAsync(string key) → Task<Dictionary<string, string>>`
  - [x] `SAddAsync(string key, string member) → Task`
  - [x] `SMembersAsync(string key) → Task<IEnumerable<string>>`
  - [x] `DeleteAsync(string key) → Task`
  - [x] `KeysAsync(string pattern) → Task<IEnumerable<string>>`
  - [x] `TtlAsync(string key) → Task<long>`
- [x] Register with `IHttpClientFactory` + Bearer token `DelegatingHandler`
- [x] Add typed exception `UpstashRedisException`

### 4.2 UpstashVectorClient

- [x] Create `IUpstashVectorClient` interface in `Knowably.Contracts`
- [x] Implement `UpstashVectorClient : IUpstashVectorClient`
  - [x] `UpsertAsync(IEnumerable<VectorRecord> records) → Task`
  - [x] `QueryAsync(float[] vector, int topK, string? filter) → Task<IEnumerable<VectorQueryResult>>`
  - [x] `FetchAsync(IEnumerable<string> ids) → Task<IEnumerable<VectorRecord>>`
  - [x] `DeleteAsync(IEnumerable<string> ids) → Task`
  - [x] `InfoAsync() → Task<VectorIndexInfo>`
- [x] Define `VectorRecord`, `VectorQueryResult`, `VectorIndexInfo` in Contracts
- [x] Register with `IHttpClientFactory` + Bearer token handler

### 4.3 QStashClient

- [x] Create `IQStashClient` interface in `Knowably.Contracts`
- [x] Implement `QStashClient : IQStashClient`
  - [x] `PublishAsync(string destinationUrl, object body, QStashOptions? options) → Task<string>`
  - [x] `GetMessageAsync(string messageId) → Task<QStashMessage>`
- [x] Define `QStashOptions` (retries, delay, deduplication ID)
- [x] Implement `QStashSignatureVerifier`
  - [x] Verify `Upstash-Signature` JWT header on `/internal/*` callbacks
  - [x] Support key rotation (current + next signing keys)
  - [x] `ClockSkew = 5 min` to tolerate QStash retry delays and minor clock drift
  - [x] Body hash mismatch is logged as a warning but not a hard rejection — JWT HMAC signature is the authoritative security check
  - [x] Structured logging for each verification step to aid debugging
  > **QStash URL note:** `QStashClient` uses absolute URLs (`https://qstash.upstash.io/v2/publish/{url}`) rather than `BaseAddress` + relative path. Setting `BaseAddress` and appending a path containing `://` causes .NET's `Uri` to drop everything before the scheme, producing an invalid URL. Never use `Uri.EscapeDataString` on the destination URL — QStash does not decode percent-encoding and rejects the request.
- [x] Register with `IHttpClientFactory`

### 4.4 Ollama Embedding Client

- [x] Implement `IEmbeddingClient` / `OllamaEmbeddingClient`
  - [x] `EmbedAsync(string text) → Task<float[]>` — POST `/api/embed`
  - [x] `EmbedBatchAsync(IEnumerable<string> texts) → Task<IEnumerable<float[]>>`
  - [x] Model: `nomic-embed-text` (768 dimensions)
- [x] Register with `IHttpClientFactory`

### 4.5 Ollama Completion Client

- [x] Implement `ICompletionClient` / `OllamaCompletionClient`
  - [x] `CompleteAsync(string systemPrompt, string userPrompt) → Task<string>` — POST `/api/chat`
  - [x] Model: `llama3.2`
- [x] Register with `IHttpClientFactory`

### 4.6 Configuration with IOptions&lt;T&gt;

Rather than reading `IConfiguration` directly, bind strongly-typed options classes. This is
more testable and idiomatic in modern .NET.

- [x] Define options classes in `Knowably.Contracts`:
  - [x] `UpstashRedisOptions` — `RestUrl`, `RestToken`
  - [x] `UpstashVectorOptions` — `RestUrl`, `RestToken`
  - [x] `QStashOptions` — `Token`, `CurrentSigningKey`, `NextSigningKey`
  - [x] `OpenAiOptions` — `ApiKey`, `EmbeddingModel`
  - [x] `AnthropicOptions` — `ApiKey`, `CompletionModel`
  - [x] `OllamaOptions` — `BaseUrl`, `EmbeddingModel`, `CompletionModel`
  - [x] `IngestionOptions` — `ChunkSize`, `ChunkOverlapPercent`, `MaxFileSizeMb`
  - [x] `CacheOptions` — `TtlSeconds`
- [x] Register each via `services.Configure<T>(configuration.GetSection("..."))` in `AddInfrastructure()`

### 4.7 DI Registration

- [x] Create `InfrastructureServiceExtensions.AddInfrastructure(this IServiceCollection)` extension method
- [x] Register all clients as singletons with named `HttpClient` instances
- [x] Bind all options via `IOptions<T>` (see 4.6)

---

## 5. Phase 2 — Core API

**Project:** `Knowably.Api`

- [x] Create ASP.NET Core Web API project (net8.0)
- [x] Add `appsettings.json` / `appsettings.Development.json` with placeholder config keys
- [x] Add `.env.example` to repo root
- [x] Add `.gitignore` covering: `.env`, `appsettings.*.json` secrets, `node_modules/`, `bin/`, `obj/`, `*.user`
- [x] Configure CORS for `http://localhost:5173` (demo) and `http://localhost:5174` (monitor)
- [x] Add global exception handling middleware → consistent `ProblemDetails` responses
- [x] Add request logging middleware
- [x] Configure Swagger / Scalar for API docs
- [x] Configure `multipart/form-data` max file size from `IngestionOptions.MaxFileSizeMb`
  - > **Note:** Upstash Redis has a 1MB per-value limit. If storing raw file content in Redis
    > temporarily (see Phase 3), enforce a file size limit well below this. Recommended default: `MAX_FILE_SIZE_MB=5` stored to local temp, not Redis.
- [x] Register `AddInfrastructure()` in `Program.cs`
- [x] Add `JsonStringEnumConverter` to controller JSON options so enum fields (e.g. `DocumentStatus`) serialise as strings (`"Indexed"`) not integers (`2`)

---

## 6. Phase 3 — Ingestion Pipeline

### 6.1 DocumentsController

- [x] `POST /documents` — accept `multipart/form-data` (file upload)
  - [x] Validate file type (PDF, TXT, MD)
  - [x] Validate file size against `IngestionOptions.MaxFileSizeMb`
  - [x] Generate document ID (`Guid`)
  - [x] **Save file to `Path.GetTempPath()/{documentId}`** — raw content must survive until the
        QStash callback fires. Do not store binary content in Redis (1MB value limit).
  - [x] Store metadata in Redis hash (`rag:doc:{id}`): `fileName`, `status=pending`, `createdAt`, `tempFilePath`
  - [x] Add ID to Redis set (`rag:doc:all`)
  - [x] Publish QStash message to `/internal/process-document` with payload `{ documentId }`
  - [x] Return `202 Accepted` with `{ documentId, statusUrl }`
- [x] `GET /documents` — list all documents from Redis
- [x] `GET /documents/{id}/status` — polling endpoint; reads Redis hash
- [x] `GET /documents/{id}/chunks` — fetches chunk vectors from Upstash Vector by docId filter
- [x] `DELETE /documents/{id}` — remove document and all associated data
  - [x] Delete Redis hash `rag:doc:{id}`
  - [x] Remove from `rag:doc:all` set
  - [x] Delete all Vector chunks where metadata `docId == {id}`
  - [x] Clean up temp file if still present

### 6.2 InternalController (QStash callbacks)

- [x] `POST /internal/process-document`
  - [x] Verify QStash signature (reject 401 if invalid)
  - [x] Read `documentId` from JSON payload
  - [x] Read `tempFilePath` from Redis hash `rag:doc:{documentId}`
  - [x] Extract text from file (raw text for TXT/MD; use `PdfPig` NuGet for PDF extraction)
  - [x] Delete temp file after text extraction
  - [x] Update Redis status → `indexing`
  - [x] Chunk text using sliding window strategy (see 6.3)
  - [x] Embed each chunk via Ollama embedding client
  - [x] Upsert to Upstash Vector with metadata: `{ docId, chunkIndex, text, source }`
  - [x] Update Redis status → `indexed`, store `chunkCount` and `indexedAt`
  - [x] On failure: update Redis status → `failed`, store `errorMessage`

### 6.3 Text Chunking (`Knowably.Ingestion`)

- [x] Implement `ITextChunker` / `SlidingWindowChunker`
  - [x] Configurable `ChunkSize` (default 512) and `Overlap` (default 10%) from `IngestionOptions`
  - [x] Returns `IEnumerable<TextChunk>` with index and text
  - [x] > **Note:** Chunk size is measured in **words** (whitespace-split), not BPE tokens.
        > This is an intentional simplification — add `Microsoft.ML.Tokenizers` later if
        > accurate token counting becomes important.

---

## 7. Phase 4 — Query Pipeline

### 7.1 QuestionsController

- [x] `POST /questions`
  - Request: `{ "question": "string" }`
  - [x] **Normalise question:** lowercase + trim + collapse whitespace before hashing
  - [x] Hash normalised question (SHA256 → hex) → Redis cache key
  - [x] Check Redis cache → if HIT: `INCR rag:stats:hits`, return with `"cached": true`
  - [x] `INCR rag:stats:queries` and `rag:stats:misses`
  - [x] Embed normalised question via Ollama embedding client
  - [x] Query Upstash Vector `top_k=5`
  - [x] Build RAG prompt with retrieved chunks as context
  - [x] Call Ollama completion client
  - [x] Cache response in Redis (`SET rag:cache:{hash}` with TTL from `CacheOptions.TtlSeconds`)
  - [x] Record per-minute stat bucket
  - [x] Return `{ answer, cached, durationMs, sourceChunks[] }`

### 7.2 Response Models (in Contracts)

- [x] `QuestionRequest`
- [x] `QuestionResponse` — `answer`, `cached`, `durationMs`, `sourceChunks[]`
- [x] `SourceChunk` — `text`, `documentId`, `chunkIndex`, `score`

---

## 8. Phase 5 — Monitor API Endpoints

- [x] `GET /stats` — read Redis counters, compute hit rate %
- [x] `GET /stats/history` — return last 60 per-minute buckets from Redis
- [x] `GET /monitor/documents` — enriched document list with chunk counts
- [x] `GET /monitor/cache` — list all `rag:cache:*` keys via `KeysAsync` scan, return with TTL and question text
- [x] `DELETE /monitor/cache/{hash}` — delete one cache entry + remove from set
- [x] `DELETE /monitor/cache` — flush all cache entries
- [x] `GET /monitor/qstash-jobs` — proxy to QStash REST API, return recent messages
- [x] `POST /monitor/vector/query` — raw vector probe: embed input, query Vector, return scored results

> All `/monitor/*` endpoints should be protected — even a simple hardcoded API key header
> (`X-Monitor-Key`) is sufficient for a portfolio project.

---

## 9. Phase 6 — Demo UI (`rag-demo`)

**Stack:** Vue 3 + TypeScript + Vite + Pinia + Vue Router + Tailwind + shadcn-vue + Axios

### 9.1 Scaffold

- [x] `npm create vue@latest rag-demo` (select TS, Router, Pinia, ESLint)
- [x] Add Tailwind CSS (v4 via @tailwindcss/vite)
- [ ] Add shadcn-vue (skipped — components hand-crafted with Tailwind)
- [x] Add Axios
- [x] Configure `vite.config.ts` proxy: `/api → http://localhost:5146`
- [x] Create `src/api/` typed API service layer mirroring C# DTOs

### 9.2 Views & Components

- [x] `DocumentUploadView`
  - [x] `FileDropZone.vue` — drag and drop, file type validation
  - [x] `UploadProgressCard.vue` — shows document ID, polls status every 2s
  - [x] `DocumentLibraryList.vue` — lists all docs with status badges
    - Status colours: pending (grey), indexing (amber), indexed (green), failed (red)
- [x] `QuestionAnswerView`
  - [x] `QuestionInput.vue` — textarea + Ask button, disabled while loading
  - [x] `AnswerCard.vue` — displays answer, "from cache" badge, duration
  - [x] `SourceChunksPanel.vue` — collapsible, shows top-k chunks with similarity scores
  - [x] `RecentQuestionsPanel.vue` — last 5 questions from `localStorage`

### 9.3 State (Pinia)

- [x] `useDocumentsStore` — document list, upload, status polling
- [x] `useQuestionsStore` — submit question, answer history

### 9.4 Routing

```
/           → redirect to /upload
/upload     → DocumentUploadView
/ask        → QuestionAnswerView
```

---

## 10. Phase 7 — Monitor UI (`rag-monitor`)

**Stack:** Same as rag-demo + Chart.js via vue-chartjs

### 10.1 Scaffold

- [ ] `npm create vue@latest rag-monitor` (select TS, Router, Pinia, ESLint)
- [ ] Add Tailwind CSS, shadcn-vue, Axios, vue-chartjs, chart.js
- [ ] Configure `vite.config.ts` proxy: `/api → http://localhost:5000`
- [ ] Create `src/api/` typed monitor API service layer
- [ ] Add `X-Monitor-Key` request interceptor in Axios

### 10.2 Views & Components

- [ ] `OverviewDashboard`
  - [ ] `StatCards.vue` — Total queries / Cache hits / Misses / Hit rate % (polled every 10s)
  - [ ] `CacheHitRateChart.vue` — line chart, last 60 minutes, via vue-chartjs
  - [ ] `QStashJobsPanel.vue` — recent ingestion jobs, delivery status, retry count
- [ ] `DocumentsView`
  - [ ] `DocumentTable.vue` — all docs, status, chunk count, indexed timestamp
  - [ ] `DocumentDetailPanel.vue` — slide-in panel; shows chunks for selected document
- [ ] `VectorExplorerView`
  - [ ] `QueryProbeInput.vue` — free-text input → hits `/monitor/vector/query`
  - [ ] `SimilarChunksTable.vue` — results with similarity scores, docId, chunk text
- [ ] `CacheView`
  - [ ] `CachedQueriesTable.vue` — question text / hash / TTL remaining
  - [ ] Invalidate buttons (single entry and flush all)

### 10.3 State (Pinia)

- [ ] `useStatsStore` — polling stats + history
- [ ] `useDocumentsStore` — document list + detail
- [ ] `useCacheStore` — cache entries, invalidation
- [ ] `useVectorExplorerStore` — probe query + results

### 10.4 Routing

```
/               → redirect to /overview
/overview       → OverviewDashboard
/documents      → DocumentsView
/vector         → VectorExplorerView
/cache          → CacheView
```

---

## 11. Phase 8 — Testing

**Project:** `Knowably.Tests` (xUnit — no Upstash account required to run)

- [ ] `SlidingWindowChunkerTests` — chunk size, overlap, edge cases (empty input, single word)
- [ ] `QuestionNormalisationTests` — casing, whitespace collapsing, trim
- [ ] `CacheKeyHashingTests` — same normalised question always produces same hash
- [ ] `QStashSignatureVerifierTests` — valid signature passes, tampered payload rejected, expired token rejected
- [ ] `TextExtractorTests` — TXT and MD extraction returns expected content

**Manual E2E checklist**
- [x] Upload a TXT document → poll until `indexed` → confirm chunk count in UI
- [x] Ask a question → verify source chunks reference the uploaded document
- [x] Ask the same question again → verify `"cached": true` in response
- [ ] Verify cache entry visible in monitor Cache view with TTL countdown
- [ ] Invalidate cache entry → ask same question again → verify fresh Ollama call
- [ ] Verify QStash signature rejection: send a POST to `/internal/process-document` without a valid signature → expect 401
- [ ] Upload a PDF → confirm text extraction and indexing succeed

---

## 12. Phase 9 — Deployment & Polish

- [ ] Add `README.md` with:
  - [ ] Architecture diagram (copy from section 1)
  - [ ] Setup instructions (Upstash console, env vars, `dotnet run`, `npm run dev`)
  - [ ] Screenshots of both UIs
  - [ ] Brief explanation of each Upstash product's role
- [ ] Add loading skeletons to both UIs
- [ ] Handle error states gracefully in both UIs (failed ingestion, API timeouts, empty states)
- [ ] Review and tighten CORS policy for production
- [ ] Consider hosting:
  - API → Railway or Fly.io (supports .NET, free tier available)
  - UIs → Vercel or Netlify (static, free tier)
  - Set `API_BASE_URL` to the deployed API host for QStash callbacks

---

## 13. Environment Variables Reference

Copy `.env.example` and populate before running:

```
# Upstash Redis
UPSTASH_REDIS_REST_URL=
UPSTASH_REDIS_REST_TOKEN=

# Upstash Vector
UPSTASH_VECTOR_REST_URL=
UPSTASH_VECTOR_REST_TOKEN=

# QStash
QSTASH_TOKEN=
QSTASH_CURRENT_SIGNING_KEY=
QSTASH_NEXT_SIGNING_KEY=

# The public URL of your API — QStash will POST to this
# For local dev, use an ngrok tunnel: ngrok http 5146
API_BASE_URL=https://your-api.example.com

# Ollama (embeddings + completions — runs locally, no API key needed)
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_EMBEDDING_MODEL=nomic-embed-text
OLLAMA_COMPLETION_MODEL=llama3.2
# Pull models first: ollama pull nomic-embed-text && ollama pull llama3.2

# Monitor UI protection
MONITOR_API_KEY=

# Ingestion limits
MAX_FILE_SIZE_MB=5
CHUNK_SIZE_WORDS=512
CHUNK_OVERLAP_PERCENT=10

# Cache TTL in seconds (default 3600)
CACHE_TTL_SECONDS=3600
```

---

## 14. NuGet & npm Dependencies

### C# (.NET 8)

| Package                        | Purpose                                       |
|--------------------------------|-----------------------------------------------|
| `PdfPig`                       | PDF text extraction                           |
| `Microsoft.Extensions.Http`    | `IHttpClientFactory` + typed clients          |
| `System.Text.Json`             | JSON serialisation                            |
| `Microsoft.IdentityModel.Tokens` | QStash JWT signature verification           |
| `Microsoft.AspNetCore.OpenApi` | Swagger / Scalar API docs                     |
| `xunit`                        | Unit testing (`Knowably.Tests`)                 |
| `xunit.runner.visualstudio`    | Test runner integration                       |
| `FluentAssertions`             | Readable test assertions                      |

> **No external AI SDK needed.** Ollama exposes a local REST API; both `OllamaEmbeddingClient`
> and `OllamaCompletionClient` use plain `HttpClient`. To switch to OpenAI or Anthropic, add the
> relevant SDK and implement `IEmbeddingClient` / `ICompletionClient`.

### Vue (both apps)

| Package          | Purpose                        |
|------------------|--------------------------------|
| `vue-router`     | Client-side routing            |
| `pinia`          | State management               |
| `axios`          | HTTP client                    |
| `tailwindcss`    | Utility CSS                    |
| `shadcn-vue`     | Headless UI components         |
| `vue-chartjs`    | Chart wrapper (monitor only)   |
| `chart.js`       | Chart engine (monitor only)    |

---

## 15. API Endpoint Reference

### Documents

| Method | Path                         | Description                          |
|--------|------------------------------|--------------------------------------|
| POST   | `/documents`                 | Upload document, queue ingestion     |
| GET    | `/documents`                 | List all documents                   |
| GET    | `/documents/{id}/status`     | Poll ingestion status                |
| GET    | `/documents/{id}/chunks`     | Fetch Vector chunks for a document   |
| DELETE | `/documents/{id}`            | Delete document + chunks + metadata  |

### Questions

| Method | Path          | Description                             |
|--------|---------------|-----------------------------------------|
| POST   | `/questions`  | Submit question, get answer + sources   |

### Stats

| Method | Path              | Description                          |
|--------|-------------------|--------------------------------------|
| GET    | `/stats`          | Redis counters + hit rate            |
| GET    | `/stats/history`  | Per-minute hit/miss buckets (60 min) |

### Monitor (protected by `X-Monitor-Key`)

| Method | Path                        | Description                          |
|--------|-----------------------------|--------------------------------------|
| GET    | `/monitor/documents`        | Enriched document list               |
| GET    | `/monitor/cache`            | All cached queries with TTLs         |
| DELETE | `/monitor/cache/{hash}`     | Invalidate single cache entry        |
| DELETE | `/monitor/cache`            | Flush all cache entries              |
| GET    | `/monitor/qstash-jobs`      | Proxied QStash delivery log          |
| POST   | `/monitor/vector/query`     | Raw vector probe (for explorer)      |

### Internal (protected by QStash signature verification)

| Method | Path                            | Description                        |
|--------|---------------------------------|------------------------------------|
| POST   | `/internal/process-document`    | QStash callback — run ingestion    |
