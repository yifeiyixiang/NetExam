using LeaveApproval.Application.Common.Markers;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace LeaveApproval.Application.Features.LeaveRequests.Commands;

/// <summary>
/// 提交请假申请（multipart/form-data 绑定）。
/// 申请人 ID 从当前登录用户上下文获取，禁止信任客户端提交的申请人/审批人信息。
/// </summary>
public class SubmitLeaveRequestCommand : IRequest<SubmitLeaveResultDto>, ITransactionalRequest
{
    public LeaveType LeaveType { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string Reason { get; set; } = null!;

    /// <summary>可选附件（multipart 中的文件列表）。</summary>
    public List<IFormFile>? Attachments { get; set; }
}

public record SubmitLeaveResultDto(
    long Id,
    string LeaveNo,
    string Status,
    int DurationHours,
    string CurrentNodeName) : IHaveLeaveNo;
