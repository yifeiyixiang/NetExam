using LeaveApproval.Application.Common;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.Common.Notifications;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using LeaveApproval.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Application.Features.LeaveRequests.Commands;

public class RejectLeaveRequestCommandHandler : IRequestHandler<RejectLeaveRequestCommand, LeaveOperationResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBaseRepository<LeaveRequest> _leaveRequests;
    private readonly IBaseRepository<User> _users;
    private readonly IBaseRepository<ApprovalRecord> _records;
    private readonly IBaseRepository<EmailOutboxMessage> _outbox;
    private readonly ICacheService _cache;
    private readonly IPostCommitExecutor _postCommit;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RejectLeaveRequestCommandHandler> _logger;

    public RejectLeaveRequestCommandHandler(
        IUnitOfWork unitOfWork,
        IBaseRepository<LeaveRequest> leaveRequests,
        IBaseRepository<User> users,
        IBaseRepository<ApprovalRecord> records,
        IBaseRepository<EmailOutboxMessage> outbox,
        ICacheService cache,
        IPostCommitExecutor postCommit,
        ICurrentUserService currentUser,
        ILogger<RejectLeaveRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _leaveRequests = leaveRequests;
        _users = users;
        _records = records;
        _outbox = outbox;
        _cache = cache;
        _postCommit = postCommit;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<LeaveOperationResultDto> Handle(RejectLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("未获取到当前用户身份");

        var leave = await _leaveRequests.Query()
            .Include(l => l.Tasks)
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("请假单", request.Id);

        if (leave.Status != LeaveStatus.PendingApproval)
        {
            throw new Domain.Exceptions.InvalidLeaveStateException($"请假单当前状态为 {leave.Status}，无法执行驳回");
        }

        var myTask = leave.Tasks.FirstOrDefault(t => t.ApproverUserId == userId);
        var task = myTask != null && myTask.WorkflowNodeId == leave.CurrentNodeId ? myTask : null;

        if (task == null)
        {
            if (myTask != null && myTask.Status is ApprovalTaskStatus.Approved or ApprovalTaskStatus.Rejected)
            {
                throw new Domain.Exceptions.TaskAlreadyProcessedException("您已处理过该请假单的审批任务，请勿重复操作");
            }

            throw new ForbiddenAccessException(myTask != null
                ? "您不是该请假单当前节点的审批人，无法越级驳回"
                : "您无权驳回该请假单");
        }

        var now = DateTime.UtcNow;
        var fromStatus = leave.Status.ToString();

        ApprovalFlow.Reject(leave, task, userId, now);
        leave.UpdatedAt = now;

        await _records.AddAsync(new ApprovalRecord
        {
            LeaveRequestId = leave.Id,
            WorkflowNodeId = task.WorkflowNodeId,
            OperatorId = userId,
            OperatorName = _currentUser.UserName ?? string.Empty,
            Action = ApprovalAction.Reject,
            Comment = request.Comment.Trim(),
            FromStatus = fromStatus,
            ToStatus = leave.Status.ToString(),
            CreatedAt = now
        });

        // 通知申请人驳回结果与意见（同事务写入 Outbox）
        var applicant = await _users.Query().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == leave.ApplicantId, cancellationToken);
        if (applicant != null)
        {
            var (subject, body) = LeaveEmailTemplates.LeaveRejected(
                leave.LeaveNo, applicant.DisplayName, task.NodeName, request.Comment.Trim());
            await _outbox.AddAsync(new EmailOutboxMessage
            {
                MessageType = "LeaveRejected",
                BusinessKey = leave.LeaveNo,
                Recipient = applicant.Email,
                Subject = subject,
                Body = body,
                Status = OutboxStatus.Pending,
                NextAttemptAt = now,
                CreatedAt = now
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "审批驳回：LeaveNo={LeaveNo}，操作人={OperatorId}，当前状态={Status}",
            leave.LeaveNo, userId, leave.Status);

        // 事务提交后失效缓存：详情 + 当前节点全部审批人的待办
        var currentNodeApproverIds = leave.Tasks
            .Where(t => t.WorkflowNodeId == task.WorkflowNodeId)
            .Select(t => t.ApproverUserId)
            .ToHashSet();

        _postCommit.Register(async token =>
        {
            await _cache.RemoveAsync(CacheKeys.Detail(request.Id), token);
            foreach (var approverId in currentNodeApproverIds)
            {
                await _cache.IncrementAsync(CacheKeys.PendingVersion(approverId), token);
            }
        });

        return new LeaveOperationResultDto(
            leave.Id,
            leave.LeaveNo,
            leave.Status.ToString(),
            "已驳回，已通知申请人");
    }
}
