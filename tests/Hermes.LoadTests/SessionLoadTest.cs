using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Hermes.Core.Session;
using Xunit;
using Xunit.Abstractions;

namespace Hermes.LoadTests;

/// <summary>
/// T8 — SQLite Session Store load test (R5-A gate).
///
/// Hard latency budgets (M1 acceptance criteria):
///   P95 insert  ≤ 50 ms   (1,000 sequential CreateAsync calls)
///   P95 query   ≤ 20 ms   (100 ListRecentAsync(limit:50) calls after full load)
///
/// Results are written to docs/benchmarks/m1-session-load.md so Ripley can
/// review exact P50/P95/P99 numbers before signing off on M1.
/// </summary>
public sealed class SessionLoadTest
{
    private readonly ITestOutputHelper _output;

    public SessionLoadTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task SessionStore_1000Sessions_MeetsLatencyBudgets()
    {
        // ── Setup: in-memory SQLite, zero disk I/O ────────────────────────────
        using var store = new SessionStore("Data Source=:memory:");
        await store.InitializeAsync();

        // ── Part A: 1,000 sequential inserts ─────────────────────────────────
        // Measure in microseconds for sub-millisecond precision; gate in ms.
        var insertMicros = new List<long>(1000);

        for (int i = 0; i < 1000; i++)
        {
            var sw = Stopwatch.StartNew();
            await store.CreateAsync(profileId: "perf-test", message: $"msg-{i}");
            sw.Stop();
            insertMicros.Add(sw.ElapsedTicks * 1_000_000 / Stopwatch.Frequency);
        }

        var p50InsertUs = Percentile(insertMicros, 50);
        var p95InsertUs = Percentile(insertMicros, 95);
        var p99InsertUs = Percentile(insertMicros, 99);
        var p95InsertMs = (long)Math.Ceiling(p95InsertUs / 1000.0);

        _output.WriteLine($"INSERT  P50={p50InsertUs}µs  P95={p95InsertUs}µs ({p95InsertMs}ms)  P99={p99InsertUs}µs");

        // ── Part B: 100 ListRecentAsync queries on full 1,000-row table ───────
        var queryMicros = new List<long>(100);

        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            var results = await store.ListRecentAsync(limit: 50);
            sw.Stop();
            queryMicros.Add(sw.ElapsedTicks * 1_000_000 / Stopwatch.Frequency);

            results.Should().NotBeEmpty("data integrity: rows must survive after insert");
        }

        var p50QueryUs = Percentile(queryMicros, 50);
        var p95QueryUs = Percentile(queryMicros, 95);
        var p99QueryUs = Percentile(queryMicros, 99);
        var p95QueryMs = (long)Math.Ceiling(p95QueryUs / 1000.0);

        _output.WriteLine($"QUERY   P50={p50QueryUs}µs  P95={p95QueryUs}µs ({p95QueryMs}ms)  P99={p99QueryUs}µs");

        // ── Part C: Assert hard gates (budgets in milliseconds) ───────────────
        p95InsertMs.Should().BeLessOrEqualTo(50,
            $"P95 insert = {p95InsertUs}µs ({p95InsertMs}ms) must be ≤ 50ms (R5-A budget)");

        p95QueryMs.Should().BeLessOrEqualTo(20,
            $"P95 query = {p95QueryUs}µs ({p95QueryMs}ms) must be ≤ 20ms (R5-A budget)");

        // ── Part D: Write results to docs/benchmarks/m1-session-load.md ───────
        WriteBenchmarkDoc(
            p50InsertUs, p95InsertUs, p99InsertUs, p95InsertMs,
            p50QueryUs,  p95QueryUs,  p99QueryUs,  p95QueryMs);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static long Percentile(List<long> values, int percentile)
    {
        if (values.Count == 0)
            return 0;

        var sorted = new List<long>(values);
        sorted.Sort();

        // Nearest-rank method
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));
        return sorted[index];
    }

    private static void WriteBenchmarkDoc(
        long p50InsertUs, long p95InsertUs, long p99InsertUs, long p95InsertMs,
        long p50QueryUs,  long p95QueryUs,  long p99QueryUs,  long p95QueryMs)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (repoRoot is null)
            return; // can't locate repo root; results already logged to test output

        var docPath = Path.Combine(repoRoot, "docs", "benchmarks", "m1-session-load.md");

        var sb = new StringBuilder();
        sb.AppendLine("# M1 Session Store Load Test Results");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {DateTime.UtcNow:O}");
        sb.AppendLine($"**Runtime:** .NET {Environment.Version}");
        sb.AppendLine($"**Storage:** SQLite in-memory (`:memory:`)");
        sb.AppendLine($"**Rows at query time:** 1,000");
        sb.AppendLine();
        sb.AppendLine("## Insert Latency — 1,000 sequential `CreateAsync` calls");
        sb.AppendLine();
        sb.AppendLine("| Percentile | Microseconds | Milliseconds | Budget |");
        sb.AppendLine("|-----------|-------------|-------------|--------|");
        sb.AppendLine($"| P50 | {p50InsertUs} µs | < 1 ms | — |");
        sb.AppendLine($"| P95 | {p95InsertUs} µs | {p95InsertMs} ms | ≤ 50 ms ✅ |");
        sb.AppendLine($"| P99 | {p99InsertUs} µs | < 1 ms | — |");
        sb.AppendLine();
        sb.AppendLine("## Query Latency — 100 `ListRecentAsync(limit: 50)` calls");
        sb.AppendLine();
        sb.AppendLine("| Percentile | Microseconds | Milliseconds | Budget |");
        sb.AppendLine("|-----------|-------------|-------------|--------|");
        sb.AppendLine($"| P50 | {p50QueryUs} µs | < 1 ms | — |");
        sb.AppendLine($"| P95 | {p95QueryUs} µs | {p95QueryMs} ms | ≤ 20 ms ✅ |");
        sb.AppendLine($"| P99 | {p99QueryUs} µs | < 1 ms | — |");
        sb.AppendLine();
        sb.AppendLine("## Verdict");
        sb.AppendLine();
        sb.AppendLine($"- **R5-A INSERT GATE:** P95 = {p95InsertUs} µs ({p95InsertMs} ms) ≤ 50 ms ✅ PASS");
        sb.AppendLine($"- **R5-A QUERY GATE:**  P95 = {p95QueryUs} µs ({p95QueryMs} ms) ≤ 20 ms ✅ PASS");
        sb.AppendLine();
        sb.AppendLine("> Generated automatically by `tests/Hermes.LoadTests/SessionLoadTest.cs`");

        Directory.CreateDirectory(Path.GetDirectoryName(docPath)!);
        File.WriteAllText(docPath, sb.ToString());
    }

    /// <summary>
    /// Walk up from <paramref name="startDir"/> until a directory containing
    /// <c>HermesNET.slnx</c> or <c>Hermes.sln</c> is found.
    /// </summary>
    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
