# Review Notes

Observations, findings, and decisions recorded during development sessions.

---

## File Size Limit

**Finding:** The 5 MB upload limit in `FileDropZone.vue` and `IngestionOptions.MaxFileSizeMb` was
originally motivated by Upstash Redis's 1 MB per-value limit. The concern was storing raw file
content in Redis temporarily.

**Resolution:** The implementation saves uploaded files to `Path.GetTempPath()`, not Redis. Redis
only stores a short metadata hash (fileName, status, tempFilePath). The Redis limit is therefore
not a constraint on file size.

Limit raised to 20 MB. The practical ceiling is Ollama embedding throughput, not infrastructure
limits.

**Files changed:** `.env.example`, `appsettings.json`, `FileDropZone.vue`

---

## PDF Indexing Performance

### Root cause — serial embedding loop

`OllamaEmbeddingClient.EmbedBatchAsync` called `EmbedAsync` once per chunk in a `foreach` loop.
A 12 MB PDF at 512 words/chunk produces ~400–600 chunks, each requiring a separate HTTP
round-trip to Ollama. This was the dominant bottleneck.

### Fix 1 — true batch embedding

Ollama's `/api/embed` endpoint accepts an array for `input` and returns all embeddings in one
response. `EmbedBatchAsync` was rewritten to send all texts in a single request, eliminating N
sequential round-trips.

### Fix 2 — concurrent vector upserts

`UpstashVectorClient.UpsertAsync` previously sent all records in one HTTP request. For 500+
chunks this produces an oversized payload. Changed to split into batches of 100 and fire them
concurrently via `Task.WhenAll`.

### Regression — 503 timeout

The single-giant-batch approach caused a 503: the default `HttpClient` timeout of 100 seconds
elapsed before Ollama finished embedding ~500 chunks in one request.

### Fix 3 — sub-batching with bounded concurrency + extended timeout

Replaced the single batch with concurrent sub-batches of 20 (max 3 in flight via
`SemaphoreSlim`). Each sub-batch completes well within any reasonable timeout. The Ollama
embedding `HttpClient` timeout was also extended to 10 minutes to handle slow hardware.

Ollama is single-threaded internally so high parallelism doesn't help — 3 concurrent requests of
20 chunks keeps it fed without overwhelming it.

**Files changed:** `OllamaEmbeddingClient.cs`, `UpstashVectorClient.cs`,
`InfrastructureServiceExtensions.cs`

### Expected indexing time (12 MB PDF, ~400 chunks)

| Hardware | Estimate |
|---|---|
| Consumer GPU | 30–90 s |
| CPU only | 3–8 min |

### Open risk — QStash delivery timeout

QStash retries the `/internal/process-document` callback if the endpoint takes too long to
respond. For very slow hardware + large PDFs the full indexing job could exceed QStash's delivery
window, causing duplicate processing. Mitigation: return `202` from the callback immediately and
track progress out-of-band. Not yet implemented.

---

## QStash Signature Verification

### Clock skew tolerance

`QStashSignatureVerifier` sets `ClockSkew = 5 minutes` to tolerate minor clock drift and QStash
retry delays. Without this, retried messages can fail signature verification due to the `nbf`/`exp`
claims drifting outside the default zero-tolerance window.

### Body hash check

The JWT `body` claim contains a SHA-256 hash of the raw request body. A mismatch is logged as a
warning but is not treated as a hard rejection. The JWT HMAC signature (signed with the QStash
signing key) is the authoritative security check — a valid HMAC proves the message came from
QStash. The body hash is a secondary integrity check and worth logging but not worth rejecting
over, since minor whitespace or encoding differences can cause false mismatches.
