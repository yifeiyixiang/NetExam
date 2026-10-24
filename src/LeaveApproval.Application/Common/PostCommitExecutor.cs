using LeaveApproval.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Application.Common;

/// <summary>事务提交后回调执行器。回调失败只记录告警，不影响业务结果。</summary>
public class PostCommitExecutor : IPostCommitExecutor
{
    private readonly List<Func<CancellationToken, Task>> _callbacks = new();
    private readonly ILogger<PostCommitExecutor> _logger;

    public PostCommitExecutor(ILogger<PostCommitExecutor> logger)
    {
        _logger = logger;
    }

    public void Register(Func<CancellationToken, Task> callback)
    {
        _callbacks.Add(callback);
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (_callbacks.Count == 0)
        {
            return;
        }

        var callbacks = _callbacks.ToArray();
        _callbacks.Clear();

        foreach (var callback in callbacks)
        {
            try
            {
                await callback(cancellationToken);
            }
            catch (Exception ex)
            {
                // 典型场景：Redis 失效失败。已记录告警并依赖缓存 TTL 兜底，不会让已成功的业务失败。
                _logger.LogWarning(ex, "事务提交后回调执行失败");
            }
        }
    }
}
