using System.Text.Json;
using FinLens.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FinLens.Infrastructure.Cache;

public class CacheService : ICacheService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly IMemoryCache? _memoryCache;
    private readonly bool _useRedis;
    private readonly ILogger<CacheService> _logger;

    public CacheService(
        ILogger<CacheService> logger,
        bool useRedis,
        IDistributedCache? distributedCache = null,
        IMemoryCache? memoryCache = null)
    {
        _logger = logger;
        _useRedis = useRedis;
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_useRedis && _distributedCache != null)
            {
                var bytes = await _distributedCache.GetAsync(key, cancellationToken);
                if (bytes == null) return default;
                return JsonSerializer.Deserialize<T>(bytes);
            }

            if (_memoryCache != null && _memoryCache.TryGetValue(key, out T? value))
                return value;

            return default;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var defaultExpiry = expiry ?? TimeSpan.FromMinutes(30);

            if (_useRedis && _distributedCache != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = defaultExpiry
                };
                await _distributedCache.SetAsync(key, bytes, options, cancellationToken);
                return;
            }

            _memoryCache?.Set(key, value, defaultExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set failed for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_useRedis && _distributedCache != null)
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
                return;
            }
            _memoryCache?.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache remove failed for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync<object>(key, cancellationToken);
        return value != null;
    }
}