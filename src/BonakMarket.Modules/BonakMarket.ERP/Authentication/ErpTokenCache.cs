using Microsoft.Extensions.Caching.Memory;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Authentication;

public sealed record ErpAccessToken(string Authorization, DateTimeOffset ExpiresAt);
public sealed class ErpTokenCache
{
    private readonly IMemoryCache _cache;
    // Fixed stripes prevent an unbounded dictionary of semaphores.
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    public ErpTokenCache(IMemoryCache cache) => _cache = cache;
    private static string Key(ErpConnection c) => $"ERP:token:{c.Id}:{c.ConfigurationVersion:N}";
    public async Task<ErpAccessToken> GetAsync(ErpConnection c, Func<Task<ErpAccessToken>> load, CancellationToken ct)
    {
        var key = Key(c);
        if (_cache.TryGetValue<ErpAccessToken>(key, out var existing) && existing.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(15))
            return existing;
        var gate = _locks[(int)((uint)key.GetHashCode() % (uint)_locks.Length)];
        await gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue<ErpAccessToken>(key, out existing) && existing.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(15))
                return existing;
            var token = await load();
            if (token.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(15))
            {
                var expiry = token.ExpiresAt.AddSeconds(-15);
                var cap = DateTimeOffset.UtcNow.AddHours(1);
                _cache.Set(key, token, expiry < cap ? expiry : cap);
            }
            return token;
        }
        finally { gate.Release(); }
    }
    public async Task InvalidateAsync(ErpConnection c, string rejected, CancellationToken ct)
    {
        var key = Key(c);
        var gate = _locks[(int)((uint)key.GetHashCode() % (uint)_locks.Length)];
        await gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue<ErpAccessToken>(key, out var current) && current.Authorization == rejected)
                _cache.Remove(key);
        }
        finally { gate.Release(); }
    }
}
