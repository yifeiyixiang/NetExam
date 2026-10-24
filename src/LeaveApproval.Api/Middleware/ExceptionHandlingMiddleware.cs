using System.Text.Json;
using FluentValidation;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Api.Middleware;

/// <summary>
/// 统一异常处理：ValidationException→400、AuthenticationFailed→401、Forbidden/审批权限→403、
/// NotFound→404、状态冲突/重复审批/并发冲突/唯一键冲突→409、未知异常→500。
/// 未处理异常在此记录完整堆栈（业务异常已由 MediatR 日志管道记录，避免重复）。
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, code, message, errors) = Map(exception);

        if (statusCode >= 500)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString();
            _logger.LogError(exception, "未处理系统异常：{ExceptionType}，CorrelationId={CorrelationId}，Path={Path}",
                exception.GetType().FullName, correlationId, context.Request.Path);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new Dictionary<string, object?>
        {
            ["traceId"] = context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier,
            ["code"] = code,
            ["message"] = message
        };

        if (errors != null)
        {
            payload["errors"] = errors;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static (int StatusCode, string Code, string Message, object? Errors) Map(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validationException:
            {
                var errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .Select(g => new
                    {
                        field = g.Key,
                        code = g.First().ErrorCode is { Length: > 0 } errorCode ? errorCode : "validation_error",
                        message = g.First().ErrorMessage
                    })
                    .ToList();
                return (400, "validation_error", "请求参数校验失败", errors);
            }

            case AuthenticationFailedException:
                return (401, "unauthorized", exception.Message, null);

            case ForbiddenAccessException:
                return (403, "forbidden", exception.Message, null);

            case ApprovalPermissionException:
                return (403, "approval_permission_denied", exception.Message, null);

            case NotFoundException:
                return (404, "not_found", exception.Message, null);

            case TaskAlreadyProcessedException:
                return (409, "task_already_processed", exception.Message, null);

            case InvalidLeaveStateException:
                return (409, "invalid_state", exception.Message, null);

            case DomainException:
                return (409, "domain_conflict", exception.Message, null);

            case ConflictException:
                return (409, "conflict", exception.Message, null);

            case DbUpdateConcurrencyException:
                return (409, "concurrency_conflict", "数据已被其他人修改，请刷新后重试", null);

            case DbUpdateException dbUpdateException when IsDuplicateKeyException(dbUpdateException):
                return (409, "duplicate_key", "数据唯一性冲突，请检查后重试", null);

            case InvalidOperationException invalidOperation:
                return (400, "invalid_operation", invalidOperation.Message, null);

            default:
                return (500, "internal_error", "服务器内部错误，请稍后重试", null);
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true ||
               ex.InnerException?.Message.Contains("1062", StringComparison.OrdinalIgnoreCase) == true;
    }
}
