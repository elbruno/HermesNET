using System.Diagnostics;
using System.Net.Http;
using Hermes.Core.Services;
using Hermes.Core.Telemetry;
using Hermes.Host;
using Hermes.Host.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace Hermes.Benchmarks;

/// <summary>
/// T10 — Performance baseline harness for ChatAsync latency.
/// Measures P50/P95/P99 turn latency (user input → response) with OTel ON.
/// Requires local Ollama at http://localhost:11434.
/// Run with: dotnet test --filter "Category=RequiresOllama"
/// </summary>
public class ChatLoopBenchmark
{
    private const string OllamaBaseUrl = "http://localhost:11434";
    private const int WarmUpRuns = 5;
    private const int MeasuredRuns = 45;
    private const int TotalRuns = WarmUpRuns + MeasuredRuns;
    private const long P95BudgetMs = 100;

    private readonly ITestOutputHelper _output;

    public ChatLoopBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// T10 acceptance test: ChatAsync("What is 2+2?") × 50 runs.
    /// Asserts P95 ≤ 100ms. Skips gracefully if Ollama is not reachable.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresOllama")]
    public async Task MeasureLatency_50Runs_P95Within100ms()
    {
        if (!await IsOllamaReachableAsync())
        {
            _output.WriteLine("⚠️  Ollama not reachable at " + OllamaBaseUrl + " — skipping benchmark.");
            _output.WriteLine("    Start Ollama with: ollama serve");
            _output.WriteLine("    Then re-run: dotnet test --filter \"Category=RequiresOllama\"");
            return; // Skip rather than fail — Ollama is a local dev dependency
        }

        // Wire OTel (console exporter) — baseline must include telemetry overhead
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Hermes.Core")
            .AddConsoleExporter()
            .Build();

        var chatService = BuildChatService();

        _output.WriteLine($"T10 — ChatAsync Latency Benchmark");
        _output.WriteLine($"Provider: Ollama ({OllamaBaseUrl})");
        _output.WriteLine($"Warm-up runs: {WarmUpRuns}, Measured runs: {MeasuredRuns}");
        _output.WriteLine(new string('-', 50));

        // Warm-up phase: not included in measurements
        _output.WriteLine("Warm-up phase...");
        for (int i = 0; i < WarmUpRuns; i++)
        {
            var turnId = Guid.NewGuid().ToString("N")[..8];
            using var _ = TelemetryProvider.StartTurnSpan(turnId);
            await chatService.ChatAsync("What is 2+2?");
        }

        // Measured phase
        var latencies = new List<long>(MeasuredRuns);
        _output.WriteLine("Measurement phase...");

        for (int i = 0; i < MeasuredRuns; i++)
        {
            var turnId = Guid.NewGuid().ToString("N")[..8];
            var sw = Stopwatch.StartNew();

            using (var turnSpan = TelemetryProvider.StartTurnSpan(turnId))
            {
                TelemetryProvider.SetMessageLength(turnSpan, "What is 2+2?".Length);

                string response;
                using (var providerSpan = TelemetryProvider.StartProviderCallSpan("Ollama"))
                {
                    response = await chatService.ChatAsync($"What is 2+2? (run {i + 1})");
                    TelemetryProvider.SetProviderLatency(providerSpan, sw.ElapsedMilliseconds);
                    TelemetryProvider.SetResponseLength(providerSpan, response.Length);
                }

                if (string.IsNullOrWhiteSpace(response))
                    throw new InvalidOperationException($"Run {i + 1}: empty response from Ollama");
            }

            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
            _output.WriteLine($"  Run {i + 1,3}: {sw.ElapsedMilliseconds,5} ms");
        }

        // Calculate percentiles
        var sorted = latencies.OrderBy(x => x).ToList();
        var p50 = Percentile(sorted, 50);
        var p95 = Percentile(sorted, 95);
        var p99 = Percentile(sorted, 99);
        var min = sorted.Min();
        var max = sorted.Max();
        var avg = sorted.Average();

        _output.WriteLine(new string('-', 50));
        _output.WriteLine($"Results ({MeasuredRuns} measured runs):");
        _output.WriteLine($"  P50 = {p50} ms");
        _output.WriteLine($"  P95 = {p95} ms  (budget: {P95BudgetMs} ms)");
        _output.WriteLine($"  P99 = {p99} ms");
        _output.WriteLine($"  Min = {min} ms  Max = {max} ms  Avg = {avg:F1} ms");

        // Persist results to docs/benchmarks/m1-perf-baseline.md
        await WriteBaselineResultsAsync(sorted, p50, p95, p99, min, max, avg);

        Assert.True(p95 <= P95BudgetMs,
            $"P95 latency {p95}ms exceeds M1 budget of {P95BudgetMs}ms. " +
            $"Check Ollama model size and host load.");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IHermesChatService BuildChatService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provider"] = "Ollama",
                ["Ollama:BaseUrl"] = OllamaBaseUrl,
                ["Ollama:Model"] = "llama2"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<ChatClientFactory>();
        services.AddSingleton<IChatClient>(sp =>
            sp.GetRequiredService<ChatClientFactory>().CreateClient());
        services.AddSingleton<IHermesChatService, HermesChatService>();

        return services.BuildServiceProvider()
                       .GetRequiredService<IHermesChatService>();
    }

    private static async Task<bool> IsOllamaReachableAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await http.GetAsync($"{OllamaBaseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static long Percentile(List<long> sorted, int percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static async Task WriteBaselineResultsAsync(
        List<long> sorted, long p50, long p95, long p99, long min, long max, double avg)
    {
        var repoRoot = FindRepositoryRoot();
        var outputPath = Path.Combine(repoRoot, "docs", "benchmarks", "m1-perf-baseline.md");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var p95Status = p95 <= P95BudgetMs ? "✅ GREEN" : "❌ OVER BUDGET";
        var p99Status = p99 <= 150 ? "✅" : "⚠️";

        var content = $"""
            # M1 Performance Baseline

            **Date:** {DateTime.UtcNow:yyyy-MM-dd}
            **Time:** {DateTime.UtcNow:HH:mm:ss} UTC
            **Environment:** Local Ollama (`llama2` model), Windows, Hermes.Core + Hermes.Host (no load test)
            **Test:** `ChatAsync("What is 2+2?")` × {WarmUpRuns + MeasuredRuns} runs ({WarmUpRuns} warm-up + {MeasuredRuns} measured)
            **OTel:** Console exporter ON (baseline includes telemetry overhead)

            ## Results

            | Metric | Value   | Budget   | Status         |
            |--------|---------|----------|----------------|
            | P50    | {p50,4} ms | —        | ✅              |
            | P95    | {p95,4} ms | {P95BudgetMs,4} ms   | {p95Status} |
            | P99    | {p99,4} ms | —        | {p99Status}             |
            | Min    | {min,4} ms | —        | —              |
            | Max    | {max,4} ms | —        | —              |
            | Avg    | {avg,4:F1} ms | —        | —              |

            ## Latency Distribution ({MeasuredRuns} measured runs)

            ```
            {string.Join(", ", sorted.Select(x => $"{x}ms"))}
            ```

            ## Measurement Definition

            - **Turn latency** = user CLI input → agent response returned (**excluding** async SQLite persist)
            - **OTel spans captured:** `hermes.chat.turn` (root), `hermes.provider.call` (child)
            - **Warm-up runs (1–{WarmUpRuns}):** excluded from percentile calculations
            - **Measured runs ({WarmUpRuns + 1}–{WarmUpRuns + MeasuredRuns}):** included in P50/P95/P99

            ## Notes

            - OTel instrumentation ON throughout (console exporter); M2 overhead gate measured relative to this
            - Zero network latency (local Ollama provider)
            - SQLite persist is async/fire-and-forget — not in turn latency gate
            - M2 regression threshold: if P95 > **{(long)(P95BudgetMs * 1.2)} ms** after T6 session store, investigate overhead

            ## M2 Regression Baseline

            | Gate           | Threshold                  | Reference    |
            |----------------|----------------------------|--------------|
            | P95 regression | > {(long)(P95BudgetMs * 1.2)} ms triggers investigation | This file     |
            | OTel overhead  | > 20% vs M1 baseline       | M2 milestone |

            ---
            *Generated by `tests/Hermes.Benchmarks/ChatLoopBenchmark.cs` — T10 acceptance test*
            """;

        await File.WriteAllTextAsync(outputPath, content);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (current.GetFiles("*.slnx").Length > 0 ||
                current.GetFiles("*.sln").Length > 0 ||
                current.GetFiles("global.json").Length > 0)
            {
                return current.FullName;
            }
            current = current.Parent!;
        }
        return AppContext.BaseDirectory;
    }
}
