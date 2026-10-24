namespace LeaveApproval.Domain.Enums;

/// <summary>请假单状态。Draft 目前未开放（可扩展），说明见 README。</summary>
public enum LeaveStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5
}
