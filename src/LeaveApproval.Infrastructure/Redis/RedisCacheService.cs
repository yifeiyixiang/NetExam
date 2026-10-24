using System.Text.Json;
using LeaveApproval.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LeaveApproval.Infrastructure.Redis;

/// <summary>
/// Redis 缓存实现（连接复用，遵循官方推荐）。
/// 所有操作容错：Redis 不可用时降级为直查数据库并记录告警，不让业务请求失败。
/// RemoveByPrefixAsync 基于 SCAN（非 KEYS）。
/// </summary>
public class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Lazy<IConnectionMultiplexer> _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(Lazy<IConnectionMultiplexer> redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var value = await Db().StringGetAsync(key);
            return value.IsNull ? null : JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 读取失败，降级为直查数据库。Key={Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value, JsonOptions);
            await Db().StringSetAsync(key, serialized, absoluteExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 写入失败。Key={Key}", key);
        }
    }

    public async Task<long?> GetLongAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await Db().StringGetAsync(key);
            return value.IsNull ? null : (long)value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 读取失败。Key={Key}", key);
            return null;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await Db().KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 删除失败，依赖缓存 TTL 兜底。Key={Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = Db();
            foreach (var server in _redis.Value.GetServers())
            {
                if (server.IsReplica)
                {
                    continue;
                }

                // Keys() 底层为 SCAN 渐进扫描，禁用 KEYS 全键扫描
                var keys = server.Keys(db.Database, pattern: prefix + "*", pageSize: 250).ToArray();
                if (keys.Length > 0)
                {
                    await db.KeyDeleteAsync(keys);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 按前缀清理失败。Prefix={Prefix}", prefix);
        }
    }

    public async Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Db().StringIncrementAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 自增失败，缓存版本失效将退化为 TTL 兜底。Key={Key}", key);
            return -1;
        }
    }

    private IDatabase Db() => _redis.Value.GetDatabase();
}
