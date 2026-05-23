using System.Runtime.CompilerServices;
using System.Text.Json;
using Hermes.Core.Services;

namespace Hermes.Host.Providers;

public class OllamaClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private const string DefaultModel = "llama2";

    public OllamaClient(string baseUrl = "http://localhost:11434", string model = DefaultModel)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _model = model;
    }

    public async ValueTask<string> CompleteAsync(
        IList<string> messages,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, stream: false);
        var jsonContent = JsonSerializer.Serialize(request);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(jsonResponse, opts);
            return ollamaResponse?.Message?.Content ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to communicate with Ollama at {_httpClient.BaseAddress}", ex);
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IList<string> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(messages, stream: true);
        var jsonContent = JsonSerializer.Serialize(request);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to communicate with Ollama at {_httpClient.BaseAddress}", ex);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new System.IO.StreamReader(stream);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line, opts);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunk?.Message?.Content is { Length: > 0 } token)
                yield return token;

            if (chunk?.Done == true)
                yield break;
        }
    }

    public void Dispose() => _httpClient?.Dispose();

    private OllamaRequest BuildRequest(IList<string> messages, bool stream) =>
        new()
        {
            Model = _model,
            Messages = messages.Select((m, i) => new OllamaMessage
            {
                Role = i == 0 ? "user" : "assistant",
                Content = m
            }).ToList(),
            Stream = stream
        };

    private sealed class OllamaRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OllamaMessage> Messages { get; set; } = new();
        public bool Stream { get; set; }
    }

    private sealed class OllamaMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaResponse
    {
        public OllamaMessage? Message { get; set; }
        public string? Model { get; set; }
        public string? CreatedAt { get; set; }
    }

    private sealed class OllamaStreamChunk
    {
        public OllamaMessage? Message { get; set; }
        public bool Done { get; set; }
    }
}