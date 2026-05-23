# M1 Session Store Load Test Results

**Date:** 2026-05-23T03:03:43.3714585Z
**Runtime:** .NET 10.0.8
**Storage:** SQLite in-memory (`:memory:`)
**Rows at query time:** 1,000

## Insert Latency — 1,000 sequential `CreateAsync` calls

| Percentile | Microseconds | Milliseconds | Budget |
|-----------|-------------|-------------|--------|
| P50 | 7 µs | < 1 ms | — |
| P95 | 13 µs | 1 ms | ≤ 50 ms ✅ |
| P99 | 19 µs | < 1 ms | — |

## Query Latency — 100 `ListRecentAsync(limit: 50)` calls

| Percentile | Microseconds | Milliseconds | Budget |
|-----------|-------------|-------------|--------|
| P50 | 83 µs | < 1 ms | — |
| P95 | 204 µs | 1 ms | ≤ 20 ms ✅ |
| P99 | 343 µs | < 1 ms | — |

## Verdict

- **R5-A INSERT GATE:** P95 = 13 µs (1 ms) ≤ 50 ms ✅ PASS
- **R5-A QUERY GATE:**  P95 = 204 µs (1 ms) ≤ 20 ms ✅ PASS

> Generated automatically by `tests/Hermes.LoadTests/SessionLoadTest.cs`
