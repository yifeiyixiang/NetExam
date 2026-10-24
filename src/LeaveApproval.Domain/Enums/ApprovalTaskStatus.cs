namespace LeaveApproval.Domain.Enums;

/// <summary>审批任务状态。Upcoming 表示流程已冻结但尚未流转到该节点。</summary>
public enum ApprovalTaskStatus
{
    Upcoming = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4,
    Skipped = 5
}
