using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.LeaveRequests.Queries;

/// <summary>下载请假单附件。访问权限与详情一致（申请人/审批人/Admin）。</summary>
public record GetLeaveAttachmentQuery(long LeaveRequestId, long AttachmentId)
    : IRequest<LeaveAttachmentFile>;
