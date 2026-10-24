using LeaveApproval.Domain.Common;
using LeaveApproval.Domain.Enums;

namespace LeaveApproval.Domain.Entities;

/// <summary>请假单。提交时冻结流程配置版本与当前节点。</summary>
public class LeaveRequest : IHasConcurrencyVersion
{
    public long Id { get; set; }

    /// <summary>业务单号，唯一索引。</summary>
    public string LeaveNo { get; set; } = null!;

    public long ApplicantId { get; set; }

    public long? DepartmentId { get; set; }

    public LeaveType LeaveType { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    /// <summary>请假时长（小时，按自然小时计算，精确到 0.1，口径见 README）。</summary>
    public decimal DurationHours { get; set; }

    public string Reason { get; set; } = null!;

    public LeaveStatus Status { get; set; }

    /// <summary>当前所处流程节点（WorkflowNode.Id），流程结束或驳回后置空。</summary>
    public long? CurrentNodeId { get; set; }

    /// <summary>当前节点顺序号，冗余存储便于排序展示。</summary>
    public int? CurrentNodeOrder { get; set; }

    /// <summary>使用的流程配置及版本快照，保证模板后续修改不影响已提交单据。</summary>
    public long WorkflowConfigId { get; set; }

    public int WorkflowVersion { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>乐观并发令牌，SaveChanges 时自增。</summary>
    public int Version { get; set; }

    public User? Applicant { get; set; }

    public Department? Department { get; set; }

    public ICollection<LeaveAttachment> Attachments { get; set; } = new List<LeaveAttachment>();

    public ICollection<LeaveApprovalTask> Tasks { get; set; } = new List<LeaveApprovalTask>();

    public ICollection<ApprovalRecord> Records { get; set; } = new List<ApprovalRecord>();
}

/// <summary>请假附件元数据。物理文件由 IFileStorageService 按 StorageKey 管理，路径不落库。</summary>
public class LeaveAttachment
{
    public long Id { get; set; }

    public long LeaveRequestId { get; set; }

    /// <summary>原始文件名仅用于展示。</summary>
    public string OriginalFileName { get; set; } = null!;

    /// <summary>随机生成的存储键，不包含用户可控路径信息。</summary>
    public string StorageKey { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long Size { get; set; }

    public long? UploadedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public LeaveRequest? LeaveRequest { get; set; }
}
