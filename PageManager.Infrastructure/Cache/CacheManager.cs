namespace PageManager.Infrastructure.Cache;

public interface ICacheManager
{
    void Invalidate(string key);

    Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan ttl,
        TimeSpan negativeTtl,
        CancellationToken ct);
}

public sealed class CacheManager(IMemoryCache memory, ILogger<CacheManager> logger) : ICacheManager
{
    public void Invalidate(string key)
    {
        memory.Remove(key);
        memory.Remove($"neg:{key}");
        logger.LogDebug("Memory cache invalidated: {Key}", key);
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan ttl,
        TimeSpan negativeTtl,
        CancellationToken ct)
    {
        if (memory.TryGetValue(key, out T? cached))
            return cached;

        if (memory.TryGetValue($"neg:{key}", out _))
            return default;

        var gate = memory.GetOrCreate($"lock:{key}", _ => new SemaphoreSlim(1, 1))!;
        await gate.WaitAsync(ct);
        try
        {
            if (memory.TryGetValue(key, out cached))
                return cached;

            if (memory.TryGetValue($"neg:{key}", out _))
                return default;

            var created = await factory(ct);
            if (created is null)
            {
                memory.Set($"neg:{key}", 1, negativeTtl);
                return default;
            }

            memory.Set(key, created, ttl);
            return created;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Memory cache invalidated: {Key}", key);
            return default;
        }
        finally
        {
            gate.Release();
        }
    }
}