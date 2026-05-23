namespace Hermes.Host.Models;

/// <summary>Consistent error response body for all 4xx/5xx responses.</summary>
public sealed class ErrorResponse
{
    public string Error { get; init; } = string.Empty;
    public string? Details { get; init; }

    public ErrorResponse(string error, string? details = null)
    {
        Error = error;
        Details = details;
    }
}
