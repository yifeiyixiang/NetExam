using LeaveApproval.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Infrastructure.Email;

/// <summary>模拟邮件服务：写结构化日志 + 内存邮箱，不依赖真实 SMTP。</summary>
public class SimulatedEmailService : IEmailService
{
    private readonly IInMemoryEmailStore _store;
    private readonly ILogger<SimulatedEmailService> _logger;

    public SimulatedEmailService(IInMemoryEmailStore store, ILogger<SimulatedEmailService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[模拟邮件] 收件人={To}，主题={Subject}，正文长度={BodyLength}",
            message.To, message.Subject, message.Body.Length);

        _store.Add(message, DateTime.UtcNow);
        return Task.CompletedTask;
    }
}

public record SentEmailRecord(EmailMessage Message, DateTime ReceivedAtUtc);

/// <summary>线程安全的内存邮箱（开发/测试环境通过 /api/dev/emails 查询）。</summary>
public class InMemoryEmailStore : IInMemoryEmailStore
{
    private readonly object _lock = new();
    private readonly Queue<SentEmailRecord> _emails = new();
    private const int Capacity = 200;

    public void Add(EmailMessage message, DateTime receivedAtUtc)
    {
        lock (_lock)
        {
            _emails.Enqueue(new SentEmailRecord(message, receivedAtUtc));
            while (_emails.Count > Capacity)
            {
                _emails.Dequeue();
            }
        }
    }

    public IReadOnlyList<(EmailMessage Message, DateTime ReceivedAtUtc)> Snapshot()
    {
        lock (_lock)
        {
            return _emails.Reverse()
                .Select(e => (e.Message, e.ReceivedAtUtc))
                .ToList();
        }
    }
}
