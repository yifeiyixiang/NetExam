using LeaveApproval.Domain.Common;
using LeaveApproval.Domain.Enums;

namespace LeaveApproval.Domain.Entities;

/// <summary>
/// 实际审批任务：提交请假单时按流程配置生成的审批人快照，
/// 与流程模板（WorkflowConfig/WorkflowNode）解耦，模板修改不影响已生成任务。
/// </summary>
public class LeaveApprovalTask : IHasConcurrencyVersion
{
    public long Id { get; set; }

    public long LeaveRequestId { get; set; }

    public long WorkflowNodeId { get; set; }

    /// <summary>节点顺序快照。</summary>
    public int NodeOrder { get; set; }

    /// <summary>节点名称快照。</summary>
    public string NodeName { get; set; } = null!;

    /// <summary>审批人快照。</summary>
    public long ApproverUserId { get; set; }

    public ApprovalTaskStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>乐观并发令牌：UPDATE 时带 Status 条件，保证并发下同一任务只能被处理一次。</summary>
    public int Version { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }

    public WorkflowNode? WorkflowNode { get; set; }

    public User? ApproverUser { get; set; }
}

/// <summary>审批操作记录：谁在何时对哪张单据执行了什么操作。</summary>
public class ApprovalRecord
{
    public long Id { get; set; }

    public long LeaveRequestId { get; set; }

    public long? WorkflowNodeId { get; set; }

    public long OperatorId { get; set; }

    /// <summary>操作人姓名快照。</summary>
    public string OperatorName { get; set; } = null!;

    public ApprovalAction Action { get; set; }

    public string? Comment { get; set; }

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }
}

/// <summary>
/// 邮件 Outbox：与业务数据同事务写入，由后台进程轮询发送并重试，
/// 保证"数据库提交成功后才会发送通知"，避免不可撤销的外部通知先于事务提交。
/// </summary>
public class EmailOutboxMessage
{
    public long Id { get; set; }

    /// <summary>消息类型，如 LeaveSubmitted / LeaveApproved / LeaveRejected / LeaveCompleted。</summary>
    public string MessageType { get; set; } = null!;

    /// <summary>业务标识（请假单号）。</summary>
    public string BusinessKey { get; set; } = null!;

    public string Recipient { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    public int Attempts { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }
}
