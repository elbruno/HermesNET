# M1 Session Store Load Test Results

**Date:** 2026-05-23T01:08:39.9082195Z
**Runtime:** .NET 10.0.8
**Storage:** SQLite in-memory (`:memory:`)
**Rows at query time:** 1,000

## Insert Latency — 1,000 sequential `CreateAsync` calls

| Percentile | Microseconds | Milliseconds | Budget |
|-----------|-------------|-------------|--------|
| P50 | 10 µs | < 1 ms | — |
| P95 | 15 µs | 1 ms | ≤ 50 ms ✅ |
| P99 | 25 µs | < 1 ms | — |

## Query Latency — 100 `ListRecentAsync(limit: 50)` calls

| Percentile | Microseconds | Milliseconds | Budget |
|-----------|-------------|-------------|--------|
| P50 | 91 µs | < 1 ms | — |
| P95 | 120 µs | 1 ms | ≤ 20 ms ✅ |
| P99 | 273 µs | < 1 ms | — |

## Verdict

- **R5-A INSERT GATE:** P95 = 15 µs (1 ms) ≤ 50 ms ✅ PASS
- **R5-A QUERY GATE:**  P95 = 120 µs (1 ms) ≤ 20 ms ✅ PASS

> Generated automatically by `tests/Hermes.LoadTests/SessionLoadTest.cs`
