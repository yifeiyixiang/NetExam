namespace LeaveApproval.Application.Common.Interfaces;

/// <summary>工作单元抽象。DbContext 是工作单元与数据库事务的唯一负责者。</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
