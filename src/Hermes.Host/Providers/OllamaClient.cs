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
        var request = new OllamaRequest
        {
            Model = _model,
            Messages = messages.Select((m, i) => new OllamaMessage
            {
                Role = i == 0 ? "user" : "assistant",
                Content = m
            }).ToList(),
            Stream = false
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(request);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var opts = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var ollamaResponse = System.Text.Json.JsonSerializer.Deserialize<OllamaResponse>(jsonResponse, opts);

            return ollamaResponse?.Message?.Content ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to communicate with Ollama at {_httpClient.BaseAddress}", ex);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private class OllamaRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OllamaMessage> Messages { get; set; } = new();
        public bool Stream { get; set; }
    }

    private class OllamaMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class OllamaResponse
    {
        public OllamaMessage? Message { get; set; }
        public string? Model { get; set; }
        public string? CreatedAt { get; set; }
    }
}
