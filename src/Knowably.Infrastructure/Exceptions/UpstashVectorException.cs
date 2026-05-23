namespace Knowably.Infrastructure.Exceptions;

public sealed class UpstashVectorException : Exception
{
    public UpstashVectorException(string message) : base(message) { }
    public UpstashVectorException(string message, Exception inner) : base(message, inner) { }
}
