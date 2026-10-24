namespace LeaveApproval.Application.Common.Interfaces;

/// <summary>
/// Redis 缓存抽象。所有实现需自行容错：Redis 不可用时降级为"无缓存"并记录日志，不得让业务请求失败。
/// 缓存键统一由 CacheKeys 管理。
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration, CancellationToken cancellationToken = default) where T : class;

    /// <summary>读取长整型计数（如待办缓存版本号），键不存在时返回 null。</summary>
    Task<long?> GetLongAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>基于 SCAN 的前缀清理（禁止使用 KEYS）。</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>原子自增，用于按用户维护待办缓存版本号实现 O(1) 批量失效。Redis 不可用时返回 -1。</summary>
    Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default);
}
