using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using LeaveApproval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Infrastructure.Email;

/// <summary>
/// Outbox 后台处理器：轮询 EmailOutboxMessages 并通过 IEmailService 发送。
/// 发送失败按指数退避重试（1/2/4/8/16 分钟），超过 5 次标记 Failed。
/// 保证：数据库事务提交成功后的邮件才可能被发送；邮件失败不会影响业务数据。
/// </summary>
public class EmailOutboxProcessor : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private const int MaxAttempts = 5;
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailOutboxProcessor> _logger;

    public EmailOutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<EmailOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("邮件 Outbox 处理器已启动，轮询间隔 {Interval}s", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件 Outbox 处理批次失败");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("邮件 Outbox 处理器已停止");
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var batch = await db.EmailOutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            try
            {
                await emailService.SendAsync(new EmailMessage(message.Recipient, message.Subject, message.Body), cancellationToken);
                message.Status = OutboxStatus.Sent;
                message.SentAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message is { Length: > 1000 } truncated ? truncated[..1000] : ex.Message;
                message.NextAttemptAt = DateTime.UtcNow + Backoff(message.Attempts);

                if (message.Attempts >= MaxAttempts)
                {
                    message.Status = OutboxStatus.Failed;
                    _logger.LogError(ex, "邮件重试 {Attempts} 次后仍失败，标记为 Failed。BusinessKey={BusinessKey}",
                        message.Attempts, message.BusinessKey);
                }
                else
                {
                    _logger.LogWarning(ex, "邮件发送失败，将在 {NextAttempt} 后重试（第 {Attempts} 次）。BusinessKey={BusinessKey}",
                        message.NextAttemptAt, message.Attempts, message.BusinessKey);
                }
            }
        }

        if (batch.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    internal static TimeSpan Backoff(int attempts) =>
        attempts switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(2),
            3 => TimeSpan.FromMinutes(4),
            4 => TimeSpan.FromMinutes(8),
            _ => TimeSpan.FromMinutes(16)
        };
}
