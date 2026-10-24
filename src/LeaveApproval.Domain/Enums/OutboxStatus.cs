namespace LeaveApproval.Domain.Enums;

/// <summary>Outbox 邮件消息状态</summary>
public enum OutboxStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}
