# M1 Session Store Load Test Results

**Date:** 2026-05-22T20:23:38.5268381Z
**Runtime:** .NET 10.0.8
**Storage:** SQLite in-memory (`:memory:`)
**Rows at query time:** 1,000

## Insert Latency — 1,000 sequential `CreateAsync` calls

| Percentile | Microseconds | Milliseconds | Budget |
|-----------|-------------|-------------|--------|
| P50 | 7 µs | < 1 ms | — |
| P95 | 12 µs | 1 ms | ≤ 50 ms ✅ |
| P99 | 16 µs | < 1 ms | — |

## Query Latency — 100 `ListRecentAsync(limit: 50)` calls

| Percentile | Microseconds | Milliseconds | Budget |
|-----------|-------------|-------------|--------|
| P50 | 84 µs | < 1 ms | — |
| P95 | 176 µs | 1 ms | ≤ 20 ms ✅ |
| P99 | 195 µs | < 1 ms | — |

## Verdict

- **R5-A INSERT GATE:** P95 = 12 µs (1 ms) ≤ 50 ms ✅ PASS
- **R5-A QUERY GATE:**  P95 = 176 µs (1 ms) ≤ 20 ms ✅ PASS

> Generated automatically by `tests/Hermes.LoadTests/SessionLoadTest.cs`
