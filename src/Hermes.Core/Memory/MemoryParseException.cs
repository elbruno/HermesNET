namespace Hermes.Core.Memory;

/// <summary>
/// Thrown when memory content fails format validation (null bytes, binary garbage,
/// or invalid control characters indicating non-text / corrupted input).
/// </summary>
public sealed class MemoryParseException : Exception
{
    public MemoryParseException(string message) : base(message) { }
    public MemoryParseException(string message, Exception inner) : base(message, inner) { }
}
