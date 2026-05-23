namespace Knowably.Infrastructure.Exceptions;

public sealed class UpstashRedisException : Exception
{
    public UpstashRedisException(string message) : base(message) { }
    public UpstashRedisException(string message, Exception inner) : base(message, inner) { }
}
