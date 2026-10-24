using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.Common.Markers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Application.Common.Behaviors;

/// <summary>
/// 事务管道（第三个执行）：仅对实现 ITransactionalRequest 的写操作开启数据库事务。
/// Handler 内调用 SaveChangesAsync，事务的提交/回滚由本管道统一负责；
/// 提交成功后才执行 PostCommit 回调（缓存失效等），保证 Redis 失效只发生在数据库提交成功之后。
/// </summary>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPostCommitExecutor _postCommitExecutor;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        IPostCommitExecutor postCommitExecutor,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _postCommitExecutor = postCommitExecutor;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
        {
            var plainResponse = await next();
            await _postCommitExecutor.ExecuteAsync(cancellationToken);
            return plainResponse;
        }

        var requestName = typeof(TRequest).Name;
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next();
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            _logger.LogDebug("事务已提交：{RequestName}", requestName);
            await _postCommitExecutor.ExecuteAsync(cancellationToken);
            return response;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogDebug("事务已回滚：{RequestName}", requestName);
            throw;
        }
    }
}
