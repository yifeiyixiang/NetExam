using LeaveApproval.Application.Common.Markers;
using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.LeaveRequests.Commands;

/// <summary>审批同意。审批人 ID 从当前用户上下文获取，不允许客户端传入。</summary>
public record ApproveLeaveRequestCommand(long Id, string? Comment)
    : IRequest<LeaveOperationResultDto>, ITransactionalRequest;
