namespace LeaveApproval.Application.Common.Models;

/// <summary>标准分页结果。</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }
}

/// <summary>附件上传限制（appsettings Attachment 节）。</summary>
public class AttachmentSettings
{
    public const string SectionName = "Attachment";

    public int MaxCount { get; set; } = 5;

    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;

    public List<string> AllowedExtensions { get; set; } = new();
}

/// <summary>请假业务限制（appsettings Leave 节）。</summary>
public class LeaveSettings
{
    public const string SectionName = "Leave";

    public decimal MaxDurationHours { get; set; } = 720;
}
