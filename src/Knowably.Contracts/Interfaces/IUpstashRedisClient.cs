namespace Knowably.Contracts.Interfaces;

public interface IUpstashRedisClient
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan? ttl = null);
    Task<long> IncrAsync(string key);
    Task HSetAsync(string key, Dictionary<string, string> fields);
    Task<Dictionary<string, string>> HGetAllAsync(string key);
    Task SAddAsync(string key, string member);
    Task<IEnumerable<string>> SMembersAsync(string key);
    Task DeleteAsync(string key);
    Task<IEnumerable<string>> KeysAsync(string pattern);
    Task<long> TtlAsync(string key);
    Task SRemAsync(string key, string member);
    Task<long> HIncrByAsync(string key, string field, long increment);
}
