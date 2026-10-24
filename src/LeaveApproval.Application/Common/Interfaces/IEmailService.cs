namespace LeaveApproval.Application.Common.Interfaces;

public record EmailMessage(string To, string Subject, string Body);

/// <summary>邮件服务抽象。开发环境由模拟实现提供（写日志 + 内存邮箱），不依赖真实 SMTP。</summary>
public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>内存邮箱（开发/测试环境查询模拟邮件），由模拟邮件服务写入。</summary>
public interface IInMemoryEmailStore
{
    void Add(EmailMessage message, DateTime receivedAtUtc);

    IReadOnlyList<(EmailMessage Message, DateTime ReceivedAtUtc)> Snapshot();
}
