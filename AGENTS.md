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
rag-demo/          rag-monitor/
(Vue 3 + TS)       (Vue 3 + TS)
     |                   |
     └─────────┬─────────┘
               ▼
       RagQnA.Api  (ASP.NET Core)
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
         Anthropic API
         (embeddings +
          completions)
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
├── RagQnA.sln
├── src/
│   ├── RagQnA.Api/                  # ASP.NET Core host, controllers, middleware
│   ├── RagQnA.Ingestion/            # Chunking, embedding orchestration
│   ├── RagQnA.Infrastructure/       # Typed HTTP clients for all three Upstash products
│   └── RagQnA.Contracts/            # DTOs, interfaces, enums (no dependencies)
├── tests/
│   └── RagQnA.Tests/                # xUnit — unit tests for pure logic (no Upstash required)
├── ui/
│   ├── rag-demo/                    # Vue 3 + TS — public demo interface
│   └── rag-monitor/                 # Vue 3 + TS — internal monitoring dashboard
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
  - Dimensions: `1536` (matches OpenAI `text-embedding-3-small`)
  - Metric: Cosine
  - Copy `UPSTASH_VECTOR_REST_URL` and `UPSTASH_VECTOR_REST_TOKEN`
- [ ] Copy `QSTASH_TOKEN` from the QStash section
- [ ] Copy `QSTASH_CURRENT_SIGNING_KEY` and `QSTASH_NEXT_SIGNING_KEY` for signature verification

> **Embedding provider note:** Anthropic does not offer an embeddings API. This project uses
> **OpenAI** (`text-embedding-3-small`, 1536 dimensions) for embeddings and **Anthropic**
> (`claude-sonnet`) for chat completions. If you switch to a different embedding model, update
> the Vector index dimensions accordingly (e.g. Cohere `embed-english-v3.0` uses 1024).

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

**Project:** `RagQnA.Infrastructure`

All three Upstash products expose a REST API — no official C# SDK exists, so you'll build
clean typed clients over `HttpClient`. This is a portfolio strength.

### 4.1 UpstashRedisClient

- [x] Create `IUpstashRedisClient` interface in `RagQnA.Contracts`
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

- [x] Create `IUpstashVectorClient` interface in `RagQnA.Contracts`
- [x] Implement `UpstashVectorClient : IUpstashVectorClient`
  - [x] `UpsertAsync(IEnumerable<VectorRecord> records) → Task`
  - [x] `QueryAsync(float[] vector, int topK, string? filter) → Task<IEnumerable<VectorQueryResult>>`
  - [x] `FetchAsync(IEnumerable<string> ids) → Task<IEnumerable<VectorRecord>>`
  - [x] `DeleteAsync(IEnumerable<string> ids) → Task`
  - [x] `InfoAsync() → Task<VectorIndexInfo>`
- [x] Define `VectorRecord`, `VectorQueryResult`, `VectorIndexInfo` in Contracts
- [x] Register with `IHttpClientFactory` + Bearer token handler

### 4.3 QStashClient

- [x] Create `IQStashClient` interface in `RagQnA.Contracts`
- [x] Implement `QStashClient : IQStashClient`
  - [x] `PublishAsync(string destinationUrl, object body, QStashOptions? options) → Task<string>`
  - [x] `GetMessageAsync(string messageId) → Task<QStashMessage>`
- [x] Define `QStashOptions` (retries, delay, deduplication ID)
- [x] Implement `QStashSignatureVerifier`
  - [x] Verify `Upstash-Signature` JWT header on `/internal/*` callbacks
  - [x] Support key rotation (current + next signing keys)
- [x] Register with `IHttpClientFactory`

### 4.4 OpenAI Embedding Client

- [x] Add `OpenAI` NuGet package
- [x] Implement `IEmbeddingClient` / `OpenAiEmbeddingClient`
  - [x] `EmbedAsync(string text) → Task<float[]>`
  - [x] `EmbedBatchAsync(IEnumerable<string> texts) → Task<IEnumerable<float[]>>`
  - [x] Model: `text-embedding-3-small` (1536 dimensions)
- [x] Register with `IHttpClientFactory`

### 4.5 Anthropic Completion Client

- [x] Add `Anthropic.SDK` NuGet package
- [x] Implement `ICompletionClient` / `AnthropicCompletionClient`
  - [x] `CompleteAsync(string systemPrompt, string userPrompt) → Task<string>`
  - [x] Model: `claude-sonnet-4-20250514`
- [x] Register with `IHttpClientFactory`

### 4.6 Configuration with IOptions&lt;T&gt;

Rather than reading `IConfiguration` directly, bind strongly-typed options classes. This is
more testable and idiomatic in modern .NET.

- [x] Define options classes in `RagQnA.Contracts`:
  - [x] `UpstashRedisOptions` — `RestUrl`, `RestToken`
  - [x] `UpstashVectorOptions` — `RestUrl`, `RestToken`
  - [x] `QStashOptions` — `Token`, `CurrentSigningKey`, `NextSigningKey`
  - [x] `OpenAiOptions` — `ApiKey`, `EmbeddingModel`
  - [x] `AnthropicOptions` — `ApiKey`, `CompletionModel`
  - [x] `IngestionOptions` — `ChunkSize`, `ChunkOverlapPercent`, `MaxFileSizeMb`
  - [x] `CacheOptions` — `TtlSeconds`
- [x] Register each via `services.Configure<T>(configuration.GetSection("..."))` in `AddInfrastructure()`

### 4.7 DI Registration

- [x] Create `InfrastructureServiceExtensions.AddInfrastructure(this IServiceCollection)` extension method
- [x] Register all clients as singletons with named `HttpClient` instances
- [x] Bind all options via `IOptions<T>` (see 4.6)

---

## 5. Phase 2 — Core API

**Project:** `RagQnA.Api`

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

---

## 6. Phase 3 — Ingestion Pipeline

### 6.1 DocumentsController

- [ ] `POST /documents` — accept `multipart/form-data` (file upload)
  - [ ] Validate file type (PDF, TXT, MD)
  - [ ] Validate file size against `IngestionOptions.MaxFileSizeMb`
  - [ ] Generate document ID (`Guid`)
  - [ ] **Save file to `Path.GetTempPath()/{documentId}`** — raw content must survive until the
        QStash callback fires. Do not store binary content in Redis (1MB value limit).
  - [ ] Store metadata in Redis hash (`rag:doc:{id}`): `fileName`, `status=pending`, `createdAt`, `tempFilePath`
  - [ ] Add ID to Redis set (`rag:doc:all`)
  - [ ] Publish QStash message to `/internal/process-document` with payload `{ documentId }`
  - [ ] Return `202 Accepted` with `{ documentId, statusUrl }`
- [ ] `GET /documents` — list all documents from Redis
- [ ] `GET /documents/{id}/status` — polling endpoint; reads Redis hash
- [ ] `GET /documents/{id}/chunks` — fetches chunk vectors from Upstash Vector by docId filter
- [ ] `DELETE /documents/{id}` — remove document and all associated data
  - [ ] Delete Redis hash `rag:doc:{id}`
  - [ ] Remove from `rag:doc:all` set
  - [ ] Delete all Vector chunks where metadata `docId == {id}`
  - [ ] Clean up temp file if still present

### 6.2 InternalController (QStash callbacks)

- [ ] `POST /internal/process-document`
  - [ ] Verify QStash signature (reject 401 if invalid)
  - [ ] Read `documentId` from JSON payload
  - [ ] Read `tempFilePath` from Redis hash `rag:doc:{documentId}`
  - [ ] Extract text from file (raw text for TXT/MD; use `PdfPig` NuGet for PDF extraction)
  - [ ] Delete temp file after text extraction
  - [ ] Update Redis status → `indexing`
  - [ ] Chunk text using sliding window strategy (see 6.3)
  - [ ] Embed each chunk via OpenAI embedding client
  - [ ] Upsert to Upstash Vector with metadata: `{ docId, chunkIndex, text, source }`
  - [ ] Update Redis status → `indexed`, store `chunkCount` and `indexedAt`
  - [ ] On failure: update Redis status → `failed`, store `errorMessage`

### 6.3 Text Chunking (`RagQnA.Ingestion`)

- [ ] Implement `ITextChunker` / `SlidingWindowChunker`
  - [ ] Configurable `ChunkSize` (default 512) and `Overlap` (default 10%) from `IngestionOptions`
  - [ ] Returns `IEnumerable<TextChunk>` with index and text
  - [ ] > **Note:** Chunk size is measured in **words** (whitespace-split), not BPE tokens.
        > This is an intentional simplification — add `Microsoft.ML.Tokenizers` later if
        > accurate token counting becomes important.

---

## 7. Phase 4 — Query Pipeline

### 7.1 QuestionsController

- [ ] `POST /questions`
  - Request: `{ "question": "string" }`
  - [ ] **Normalise question:** lowercase + trim + collapse whitespace before hashing
  - [ ] Hash normalised question (SHA256 → hex) → Redis cache key
  - [ ] Check Redis cache → if HIT: `INCR rag:stats:hits`, return with `"cached": true`
  - [ ] `INCR rag:stats:queries` and `rag:stats:misses`
  - [ ] Embed normalised question via OpenAI embedding client
  - [ ] Query Upstash Vector `top_k=5`
  - [ ] Build RAG prompt with retrieved chunks as context
  - [ ] Call Anthropic completion client
  - [ ] Cache response in Redis (`SET rag:cache:{hash}` with TTL from `CacheOptions.TtlSeconds`)
  - [ ] Record per-minute stat bucket
  - [ ] Return `{ answer, cached, durationMs, sourceChunks[] }`

### 7.2 Response Models (in Contracts)

- [ ] `QuestionRequest`
- [ ] `QuestionResponse` — `answer`, `cached`, `durationMs`, `sourceChunks[]`
- [ ] `SourceChunk` — `text`, `documentId`, `chunkIndex`, `score`

---

## 8. Phase 5 — Monitor API Endpoints

- [ ] `GET /stats` — read Redis counters, compute hit rate %
- [ ] `GET /stats/history` — return last 60 per-minute buckets from Redis
- [ ] `GET /monitor/documents` — enriched document list with chunk counts
- [ ] `GET /monitor/cache` — list all `rag:cache:*` keys via `KeysAsync` scan, return with TTL and question text
- [ ] `DELETE /monitor/cache/{hash}` — delete one cache entry + remove from set
- [ ] `DELETE /monitor/cache` — flush all cache entries
- [ ] `GET /monitor/qstash-jobs` — proxy to QStash REST API, return recent messages
- [ ] `POST /monitor/vector/query` — raw vector probe: embed input, query Vector, return scored results

> All `/monitor/*` endpoints should be protected — even a simple hardcoded API key header
> (`X-Monitor-Key`) is sufficient for a portfolio project.

---

## 9. Phase 6 — Demo UI (`rag-demo`)

**Stack:** Vue 3 + TypeScript + Vite + Pinia + Vue Router + Tailwind + shadcn-vue + Axios

### 9.1 Scaffold

- [ ] `npm create vue@latest rag-demo` (select TS, Router, Pinia, ESLint)
- [ ] Add Tailwind CSS
- [ ] Add shadcn-vue
- [ ] Add Axios
- [ ] Configure `vite.config.ts` proxy: `/api → http://localhost:5000`
- [ ] Create `src/api/` typed API service layer mirroring C# DTOs

### 9.2 Views & Components

- [ ] `DocumentUploadView`
  - [ ] `FileDropZone.vue` — drag and drop, file type validation
  - [ ] `UploadProgressCard.vue` — shows document ID, polls status every 2s
  - [ ] `DocumentLibraryList.vue` — lists all docs with status badges
    - Status colours: pending (grey), indexing (amber), indexed (green), failed (red)
- [ ] `QuestionAnswerView`
  - [ ] `QuestionInput.vue` — textarea + Ask button, disabled while loading
  - [ ] `AnswerCard.vue` — displays answer, "from cache" badge, duration
  - [ ] `SourceChunksPanel.vue` — collapsible, shows top-k chunks with similarity scores
  - [ ] `RecentQuestionsPanel.vue` — last 5 questions from `localStorage`

### 9.3 State (Pinia)

- [ ] `useDocumentsStore` — document list, upload, status polling
- [ ] `useQuestionsStore` — submit question, answer history

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

**Project:** `RagQnA.Tests` (xUnit — no Upstash account required to run)

- [ ] `SlidingWindowChunkerTests` — chunk size, overlap, edge cases (empty input, single word)
- [ ] `QuestionNormalisationTests` — casing, whitespace collapsing, trim
- [ ] `CacheKeyHashingTests` — same normalised question always produces same hash
- [ ] `QStashSignatureVerifierTests` — valid signature passes, tampered payload rejected, expired token rejected
- [ ] `TextExtractorTests` — TXT and MD extraction returns expected content

**Manual E2E checklist**
- [ ] Upload a TXT document → poll until `indexed` → confirm chunk count in monitor
- [ ] Ask a question → verify source chunks reference the uploaded document
- [ ] Ask the same question again → verify `"cached": true` in response
- [ ] Verify cache entry visible in monitor Cache view with TTL countdown
- [ ] Invalidate cache entry → ask same question again → verify fresh Anthropic call
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
API_BASE_URL=https://your-api.example.com

# OpenAI (embeddings)
OPENAI_API_KEY=
OPENAI_EMBEDDING_MODEL=text-embedding-3-small

# Anthropic (completions)
ANTHROPIC_API_KEY=
ANTHROPIC_COMPLETION_MODEL=claude-sonnet-4-20250514

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
| `OpenAI`                       | Embeddings (`text-embedding-3-small`)         |
| `Anthropic.SDK`                | Chat completions (`claude-sonnet`)            |
| `PdfPig`                       | PDF text extraction                           |
| `Microsoft.Extensions.Http`    | `IHttpClientFactory` + typed clients          |
| `System.Text.Json`             | JSON serialisation                            |
| `Microsoft.AspNetCore.OpenApi` | Swagger / Scalar API docs                     |
| `xunit`                        | Unit testing (`RagQnA.Tests`)                 |
| `xunit.runner.visualstudio`    | Test runner integration                       |
| `FluentAssertions`             | Readable test assertions                      |

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
