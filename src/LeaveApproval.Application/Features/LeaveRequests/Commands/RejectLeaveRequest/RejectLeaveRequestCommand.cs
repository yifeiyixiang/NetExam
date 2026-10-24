using LeaveApproval.Application.Common.Markers;
using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.LeaveRequests.Commands;

/// <summary>审批驳回。驳回意见必填。</summary>
public record RejectLeaveRequestCommand(long Id, string Comment)
    : IRequest<LeaveOperationResultDto>, ITransactionalRequest;
