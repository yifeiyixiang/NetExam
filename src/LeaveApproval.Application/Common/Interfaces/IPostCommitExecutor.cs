namespace LeaveApproval.Application.Common.Interfaces;

/// <summary>事务提交成功后执行的回调（缓存失效、清理等）。失败不回滚事务，仅记录日志。</summary>
public interface IPostCommitExecutor
{
    void Register(Func<CancellationToken, Task> callback);

    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
