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
            var ollamaResponse = System.Text.Json.JsonSerializer.Deserialize<OllamaResponse>(jsonResponse);

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
        public required string Model { get; set; }
        public required List<OllamaMessage> Messages { get; set; }
        public bool Stream { get; set; }
    }

    private class OllamaMessage
    {
        public required string Role { get; set; }
        public required string Content { get; set; }
    }

    private class OllamaResponse
    {
        public OllamaMessage? Message { get; set; }
        public string? Model { get; set; }
        public string? CreatedAt { get; set; }
    }
}
