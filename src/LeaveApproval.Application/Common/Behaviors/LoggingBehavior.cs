using System.Diagnostics;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.Common.Markers;
using MediatR;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace LeaveApproval.Application.Common.Behaviors;

/// <summary>
/// 请求日志管道（第一个执行）：记录请求名称、当前操作人、耗时与结果。
/// 业务异常在此记录一次；未预期系统异常由全局异常中间件记录，避免重复记录。
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger, ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        using (LogContext.PushProperty("MediatRRequest", requestName))
        using (LogContext.PushProperty("OperatorId", _currentUser.UserId))
        using (LogContext.PushProperty("OperatorName", _currentUser.UserName))
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("开始处理 {RequestName}（操作人：{OperatorId}/{OperatorName}）",
                requestName, _currentUser.UserId, _currentUser.UserName);
            try
            {
                var response = await next();
                stopwatch.Stop();

                using (response is IHaveLeaveNo withLeaveNo
                           ? LogContext.PushProperty("LeaveNo", withLeaveNo.LeaveNo)
                           : null)
                {
                    _logger.LogInformation("处理完成 {RequestName}，耗时 {ElapsedMilliseconds}ms，结果：成功",
                        requestName, stopwatch.ElapsedMilliseconds);
                }

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "处理失败 {RequestName}，耗时 {ElapsedMilliseconds}ms，异常类型：{ExceptionType}，原因：{ErrorMessage}",
                    requestName, stopwatch.ElapsedMilliseconds, ex.GetType().Name, ex.Message);
                throw;
            }
        }
    }
}
