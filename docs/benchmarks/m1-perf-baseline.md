# M1 Performance Baseline

**Date:** 2026-05-22
**Environment:** Local Ollama (`llama2` model), Windows, Hermes.Core + Hermes.Host (no load test)
**Test:** `ChatAsync("What is 2+2?")` × 50 runs (5 warm-up + 45 measured)
**OTel:** Console exporter ON (baseline includes telemetry overhead)
**Harness:** `tests/Hermes.Benchmarks/ChatLoopBenchmark.cs` — Stopwatch / xUnit (Option B)

## Status

> ⚠️ **PENDING REAL OLLAMA RUN** — Harness is complete and builds/passes. Results below require
> `ollama serve` to be running. Run `dotnet test tests/Hermes.Benchmarks --filter "Category=RequiresOllama"`
> to populate real measurements and commit updated numbers.

## T4 Simulated Baseline (Context)

From T4 OTel instrumentation work (simulated Ollama, 10 iterations):

| Metric | Value  | Budget   | Status    |
|--------|--------|----------|-----------|
| P95    | 55 ms  | 100 ms   | ✅ GREEN  |
| Min    | 29 ms  | —        | ✅        |
| Max    | 55 ms  | —        | ✅        |
| Avg    | 43 ms  | —        | ✅        |

*Source: `M1-BASELINE.txt` (T4 OTel baseline, 2026-05-22)*

## Real Ollama Results (populate when run)

| Metric | Value | Budget   | Status         |
|--------|-------|----------|----------------|
| P50    | TBD   | —        | —              |
| P95    | TBD   | 100 ms   | —              |
| P99    | TBD   | —        | —              |
| Min    | TBD   | —        | —              |
| Max    | TBD   | —        | —              |
| Avg    | TBD   | —        | —              |

## Measurement Definition

- **Turn latency** = user CLI input → agent response returned (**excluding** async SQLite persist)
- **OTel spans captured:** `hermes.chat.turn` (root), `hermes.provider.call` (child)
- **Warm-up runs (1–5):** excluded from percentile calculations — allows JIT + Ollama model warm-up
- **Measured runs (6–50):** included in P50/P95/P99

## Harness Setup

```csharp
// DI wiring in ChatLoopBenchmark.BuildChatService()
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> {
        ["Provider"]          = "Ollama",
        ["Ollama:BaseUrl"]    = "http://localhost:11434",
        ["Ollama:Model"]      = "llama2"
    }).Build();

services.AddSingleton<IConfiguration>(config);
services.AddSingleton<ChatClientFactory>();
services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<ChatClientFactory>().CreateClient());
services.AddSingleton<IHermesChatService, HermesChatService>();
```

OTel wired per turn:
```csharp
using (var turnSpan = TelemetryProvider.StartTurnSpan(turnId))
using (var providerSpan = TelemetryProvider.StartProviderCallSpan("Ollama"))
{
    response = await chatService.ChatAsync("What is 2+2?");
}
sw.Stop();
latencies.Add(sw.ElapsedMilliseconds);
```

## Notes

- OTel instrumentation ON throughout; M2 overhead gate measured relative to this file
- Zero network latency (local Ollama provider)
- SQLite persist is async/fire-and-forget — not in turn latency gate
- M2 regression threshold: if P95 > **120 ms** after T6 session store is wired, investigate overhead

## M2 Regression Baseline

| Gate           | Threshold                    | Reference     |
|----------------|------------------------------|---------------|
| P95 regression | > 120 ms triggers investigation | This file  |
| OTel overhead  | > 20% vs M1 baseline         | M2 milestone  |

## How to Run

```bash
# Start Ollama with llama2 model
ollama serve
ollama pull llama2   # if not already pulled

# Run the benchmark (requires Ollama running)
cd D:\elbruno\HermesNET
dotnet test tests/Hermes.Benchmarks --filter "Category=RequiresOllama" --logger "console;verbosity=detailed"
```

Results will be automatically written to this file by `WriteBaselineResultsAsync()`.

---
*Harness created by Parker (T10) — 2026-05-22. Feeds into T11 E2E OTel walk and R5 Go/No-Go.*
