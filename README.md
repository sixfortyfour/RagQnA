# Knowably

A full-stack portfolio project demonstrating **Retrieval-Augmented Generation (RAG)** using all three [Upstash](https://upstash.com) products (Redis, Vector, QStash), an ASP.NET Core Web API, and a Vue 3 + TypeScript frontend.

Documents are chunked, embedded via a local [Ollama](https://ollama.com) model, and stored in Upstash Vector for semantic search. Questions are answered by a local LLM grounded in the retrieved context, with results cached in Redis to avoid redundant inference calls.

---

## Architecture

```
[Knowably UI]
 knowably/
 (Vue 3 + TS)
      |
      ▼
Knowably.Api  (ASP.NET Core — port 5146)
┌────────────────────────────────────┐
│  POST /documents                   │  ← upload file
│  POST /questions                   │  ← ask question
│  GET  /stats                       │  ← cache counters
│  GET  /monitor/*                   │  ← admin endpoints
│  POST /internal/process-document   │  ← QStash callback
└────────┬───────────────────────────┘
         │
 ┌───────┼──────────────┐
 ▼       ▼              ▼
Redis   Vector        QStash
(cache  (embeddings   (async
+ meta)  + search)    ingestion)
              ▲
           Ollama
     (embeddings + completions,
          runs locally)
```

### Ingestion flow (async)
```
POST /documents → 202 Accepted
  → store metadata in Redis (status: pending)
  → publish job to QStash
  → QStash calls /internal/process-document
  → chunk → embed → upsert to Vector
  → update Redis status to indexed
```

### Query flow (synchronous, cached)
```
POST /questions
  → hash question → Redis cache check
  → HIT:  return cached answer immediately
  → MISS: embed → Vector query (top 5 chunks)
          → build prompt → Ollama completion
          → cache in Redis with TTL
          → return answer + source chunks
```

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ | `dotnet --version` to verify |
| [Node.js](https://nodejs.org) | 20.19+ or 22.12+ | `node --version` to verify |
| [Ollama](https://ollama.com) | Latest | Runs the local LLM |
| [ngrok](https://ngrok.com) | Latest | Exposes the local API to QStash |

---

## 1. Upstash setup

Sign up for a free account at [console.upstash.com](https://console.upstash.com).

### Redis
1. Create a **Redis** database (any region)
2. Copy **REST URL** and **REST Token** from the database page

### Vector
1. Create a **Vector** index with these settings:
   - **Dimensions:** `768` (matches `nomic-embed-text`)
   - **Metric:** Cosine
2. Copy **REST URL** and **REST Token**

### QStash
1. Navigate to the **QStash** section
2. Copy **Token**, **Current Signing Key**, and **Next Signing Key**

---

## 2. Ollama setup

```bash
# Install Ollama from https://ollama.com, then pull the required models:
ollama pull nomic-embed-text   # embeddings (768 dimensions)
ollama pull llama3.2           # completions
```

Ollama runs on `http://localhost:11434` by default. Leave it running in the background.

---

## 3. ngrok setup

QStash needs a publicly reachable URL to POST callbacks to your local API.

```bash
# Install ngrok from https://ngrok.com, then:
ngrok http 5146
```

Copy the `https://` forwarding URL (e.g. `https://abc123.ngrok-free.app`) — you will need it for the `ApiBaseUrl` config key below.

> **Note:** The ngrok URL changes each time you restart unless you have a paid/reserved domain. Update `ApiBaseUrl` in your config whenever it changes.

---

## 4. API configuration

The API reads configuration from `appsettings.json` (committed, safe defaults only). Secrets are stored using [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets), which keeps credentials out of the repository entirely.

```bash
cd src/Knowably.Api

dotnet user-secrets set "UpstashRedis:RestUrl"        "https://<your-redis-url>.upstash.io"
dotnet user-secrets set "UpstashRedis:RestToken"      "<your-redis-rest-token>"

dotnet user-secrets set "UpstashVector:RestUrl"       "https://<your-vector-url>.upstash.io"
dotnet user-secrets set "UpstashVector:RestToken"     "<your-vector-rest-token>"

dotnet user-secrets set "QStash:Token"                "<your-qstash-token>"
dotnet user-secrets set "QStash:CurrentSigningKey"    "<your-current-signing-key>"
dotnet user-secrets set "QStash:NextSigningKey"       "<your-next-signing-key>"

dotnet user-secrets set "Monitor:ApiKey"              "<your-monitor-key>"
dotnet user-secrets set "ApiBaseUrl"                  "https://<your-ngrok-url>.ngrok-free.app"
```

User Secrets are stored in your OS user profile (not the repo) and are automatically loaded in the `Development` environment. The Ollama, Ingestion, and Cache sections have safe defaults in `appsettings.json` and only need overriding if you want non-default values.

| Key | Description |
|-----|-------------|
| `UpstashRedis.RestUrl` / `RestToken` | From the Redis database page in the Upstash console |
| `UpstashVector.RestUrl` / `RestToken` | From the Vector index page |
| `QStash.Token` | Used to publish jobs to QStash |
| `QStash.CurrentSigningKey` | Used to verify QStash callbacks |
| `QStash.NextSigningKey` | Used during key rotation |
| `Ollama.BaseUrl` | Ollama base URL (default `http://localhost:11434`) |
| `Ollama.EmbeddingModel` | Must match the dimension count of your Vector index (`nomic-embed-text` = 768) |
| `Ollama.CompletionModel` | Chat completion model |
| `Ingestion.ChunkSize` | Words per chunk (default 512) |
| `Ingestion.ChunkOverlapPercent` | Sliding window overlap % (default 10) |
| `Ingestion.MaxFileSizeMb` | Upload size limit in MB (default 5) |
| `Cache.TtlSeconds` | Redis answer cache TTL in seconds (default 3600) |
| `Monitor.ApiKey` | Arbitrary secret — sent as `X-Monitor-Key` header to access `/monitor/*` endpoints |
| `ApiBaseUrl` | Public URL of your API — QStash POSTs callbacks here |

---

## 5. Running the API

```bash
cd src/Knowably.Api
dotnet run
```

The API starts on **http://localhost:5146**.

Interactive API docs are available at **http://localhost:5146/scalar** (development only).

---

## 6. Running the frontend

```bash
cd ui/knowably
npm install
npm run dev
```

The UI starts on **http://localhost:5173** and proxies `/api/*` requests to the API at `http://localhost:5146`.

---

## 7. Verifying the pipeline

1. **Upload** a TXT, PDF, or Markdown file on the Upload page
2. The document status cycles: `Queued → Indexing → Ready`
   - If it stays on `Queued`, check that ngrok is running and `ApiBaseUrl` is current
3. **Ask** a question about the document on the Ask page
4. The answer includes source chunks from the document
5. **Ask the same question again** — the response should show `From cache` and return near-instantly

---

## Project structure

```
Knowably/
├── src/
│   ├── Knowably.Api/           # ASP.NET Core host, controllers, middleware
│   ├── Knowably.Contracts/     # DTOs, interfaces, enums — no dependencies
│   ├── Knowably.Infrastructure/ # Typed HTTP clients for Upstash + Ollama
│   └── Knowably.Ingestion/     # Text chunking and extraction
├── tests/
│   └── Knowably.Tests/         # xUnit unit tests
├── ui/
│   └── knowably/             # Vue 3 + TypeScript — Knowably frontend
└── Knowably.sln
```

---

## API reference

### Documents

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/documents` | Upload a file, queue async ingestion |
| `GET` | `/documents` | List all documents |
| `GET` | `/documents/{id}/status` | Poll ingestion status |
| `GET` | `/documents/{id}/chunks` | Fetch Vector chunks for a document |
| `DELETE` | `/documents/{id}` | Delete document, chunks, and metadata |

### Questions

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/questions` | Submit a question, receive answer + source chunks |

### Stats

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/stats` | Redis counters and cache hit rate |
| `GET` | `/stats/history` | Per-minute hit/miss buckets (last 60 min) |

### Monitor (requires `X-Monitor-Key` header)

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/monitor/documents` | Enriched document list |
| `GET` | `/monitor/cache` | All cached queries with TTLs |
| `DELETE` | `/monitor/cache/{hash}` | Invalidate a single cache entry |
| `DELETE` | `/monitor/cache` | Flush all cache entries |
| `GET` | `/monitor/qstash-jobs` | Recent QStash delivery log |
| `POST` | `/monitor/vector/query` | Raw vector probe |

### Internal (verified by QStash signature)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/internal/process-document` | QStash callback — runs ingestion |

---

## Tech stack

| Layer | Technology |
|-------|-----------|
| API | ASP.NET Core (.NET 8), C# |
| Embeddings | Ollama `nomic-embed-text` (768 dimensions) |
| Completions | Ollama `llama3.2` |
| Vector store | Upstash Vector (cosine similarity) |
| Cache + metadata | Upstash Redis |
| Async ingestion | Upstash QStash |
| Frontend | Vue 3, TypeScript, Vite, Pinia, Tailwind CSS v4 |
