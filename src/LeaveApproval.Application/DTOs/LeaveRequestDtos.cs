using LeaveApproval.Application.Common.Markers;

namespace LeaveApproval.Application.DTOs;

public class UserSummaryDto
{
    public long Id { get; set; }

    public string Username { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string? DepartmentName { get; set; }
}

public class ApprovalTaskDto
{
    public long Id { get; set; }

    public int NodeOrder { get; set; }

    public string NodeName { get; set; } = null!;

    public long ApproverUserId { get; set; }

    public string? ApproverName { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CompletedAt { get; set; }
}

public class ApprovalRecordDto
{
    public long Id { get; set; }

    public string Action { get; set; } = null!;

    public string OperatorName { get; set; } = null!;

    public string? Comment { get; set; }

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class LeaveAttachmentDto
{
    public long Id { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long Size { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>安全访问地址（相对路径），不暴露物理磁盘路径。</summary>
    public string DownloadUrl { get; set; } = null!;
}

/// <summary>请假单详情（优先来自 Redis 缓存，故包含权限判定所需的申请人/审批人标识）。</summary>
public class LeaveRequestDetailDto : IHaveLeaveNo
{
    public long Id { get; set; }

    public string LeaveNo { get; set; } = null!;

    public string LeaveType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public decimal DurationHours { get; set; }

    public string Reason { get; set; } = null!;

    public UserSummaryDto? Applicant { get; set; }

    public string? DepartmentName { get; set; }

    public int? CurrentNodeOrder { get; set; }

    public string? CurrentNodeName { get; set; }

    public int WorkflowVersion { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public IReadOnlyList<ApprovalTaskDto> Tasks { get; set; } = Array.Empty<ApprovalTaskDto>();

    public IReadOnlyList<ApprovalRecordDto> Records { get; set; } = Array.Empty<ApprovalRecordDto>();

    public IReadOnlyList<LeaveAttachmentDto> Attachments { get; set; } = Array.Empty<LeaveAttachmentDto>();
}

public class LeaveRequestListItemDto : IHaveLeaveNo
{
    public long Id { get; set; }

    public string LeaveNo { get; set; } = null!;

    public string LeaveType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public decimal DurationHours { get; set; }

    public string Reason { get; set; } = null!;

    public int? CurrentNodeOrder { get; set; }

    public string? CurrentNodeName { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>我的待办列表项。</summary>
public class PendingApprovalItemDto : IHaveLeaveNo
{
    public long LeaveRequestId { get; set; }

    public string LeaveNo { get; set; } = null!;

    public string ApplicantName { get; set; } = null!;

    public string? DepartmentName { get; set; }

    public string LeaveType { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public decimal DurationHours { get; set; }

    public string Reason { get; set; } = null!;

    public long TaskId { get; set; }

    public int NodeOrder { get; set; }

    public string NodeName { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }
}

/// <summary>提交/审批/驳回等操作的统一返回。</summary>
public record LeaveOperationResultDto(long Id, string LeaveNo, string Status, string Message) : IHaveLeaveNo;

/// <summary>附件下载结果（Handler 打开只读流，Controller 转为 FileStreamResult 并负责释放）。</summary>
public record LeaveAttachmentFile(Stream Stream, string DownloadName, string ContentType);
