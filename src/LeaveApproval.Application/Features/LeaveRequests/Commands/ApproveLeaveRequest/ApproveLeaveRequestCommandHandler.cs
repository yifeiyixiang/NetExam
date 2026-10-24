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

public class ApproveLeaveRequestCommandHandler : IRequestHandler<ApproveLeaveRequestCommand, LeaveOperationResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBaseRepository<LeaveRequest> _leaveRequests;
    private readonly IBaseRepository<User> _users;
    private readonly IBaseRepository<ApprovalRecord> _records;
    private readonly IBaseRepository<EmailOutboxMessage> _outbox;
    private readonly ICacheService _cache;
    private readonly IPostCommitExecutor _postCommit;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ApproveLeaveRequestCommandHandler> _logger;

    public ApproveLeaveRequestCommandHandler(
        IUnitOfWork unitOfWork,
        IBaseRepository<LeaveRequest> leaveRequests,
        IBaseRepository<User> users,
        IBaseRepository<ApprovalRecord> records,
        IBaseRepository<EmailOutboxMessage> outbox,
        ICacheService cache,
        IPostCommitExecutor postCommit,
        ICurrentUserService currentUser,
        ILogger<ApproveLeaveRequestCommandHandler> logger)
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

    public async Task<LeaveOperationResultDto> Handle(ApproveLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("未获取到当前用户身份");

        // 跟踪查询：需要修改实体并依赖乐观并发令牌（Version 条件更新）
        var leave = await _leaveRequests.Query()
            .Include(l => l.Tasks)
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("请假单", request.Id);

        if (leave.Status != LeaveStatus.PendingApproval)
        {
            throw new Domain.Exceptions.InvalidLeaveStateException($"请假单当前状态为 {leave.Status}，无法执行审批");
        }

        // 对象级授权：必须是当前节点的有效审批人（Admin 也不能代替节点审批人）
        var myTask = leave.Tasks.FirstOrDefault(t => t.ApproverUserId == userId);
        var task = myTask != null && myTask.WorkflowNodeId == leave.CurrentNodeId ? myTask : null;

        if (task == null)
        {
            if (myTask != null && myTask.Status is ApprovalTaskStatus.Approved or ApprovalTaskStatus.Rejected)
            {
                throw new Domain.Exceptions.TaskAlreadyProcessedException("您已处理过该请假单的审批任务，请勿重复操作");
            }

            throw new ForbiddenAccessException(myTask != null
                ? "您不是该请假单当前节点的审批人，无法越级审批"
                : "您无权审批该请假单");
        }

        var now = DateTime.UtcNow;
        var fromStatus = leave.Status.ToString();

        var finished = ApprovalFlow.Approve(leave, task, userId, now);
        leave.UpdatedAt = now;

        await _records.AddAsync(new ApprovalRecord
        {
            LeaveRequestId = leave.Id,
            WorkflowNodeId = task.WorkflowNodeId,
            OperatorId = userId,
            OperatorName = _currentUser.UserName ?? string.Empty,
            Action = ApprovalAction.Approve,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment!.Trim(),
            FromStatus = fromStatus,
            ToStatus = leave.Status.ToString(),
            CreatedAt = now
        });

        // 待失效缓存所涉及的用户：当前节点全部审批人 + 下一节点审批人（如有）
        var currentNodeApproverIds = leave.Tasks
            .Where(t => t.WorkflowNodeId == task.WorkflowNodeId)
            .Select(t => t.ApproverUserId)
            .ToList();

        var nextApproverIds = finished
            ? new List<long>()
            : leave.Tasks
                .Where(t => t.NodeOrder == task.NodeOrder + 1)
                .Select(t => t.ApproverUserId)
                .ToList();

        // Outbox 通知（与业务数据同事务写入）
        if (finished)
        {
            var applicant = await _users.Query().AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == leave.ApplicantId, cancellationToken);
            if (applicant != null)
            {
                var (subject, body) = LeaveEmailTemplates.LeaveCompleted(leave.LeaveNo, applicant.DisplayName);
                await _outbox.AddAsync(BuildOutbox("LeaveCompleted", leave.LeaveNo, applicant.Email, subject, body, now));
            }
        }
        else
        {
            var nextApprovers = await LoadActiveUsersAsync(nextApproverIds, cancellationToken);
            foreach (var approver in nextApprovers)
            {
                var (subject, body) = LeaveEmailTemplates.LeaveApprovedNextNode(
                    leave.LeaveNo, await GetApplicantNameAsync(leave.ApplicantId, cancellationToken),
                    approver.DisplayName, task.NodeName);
                await _outbox.AddAsync(BuildOutbox("LeaveApprovedNextNode", leave.LeaveNo, approver.Email, subject, body, now));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "审批通过：LeaveNo={LeaveNo}，操作人={OperatorId}，流程完成={Finished}，当前状态={Status}",
            leave.LeaveNo, userId, finished, leave.Status);

        // 事务提交后失效缓存
        var affectedUserIds = currentNodeApproverIds.Union(nextApproverIds).ToHashSet();
        _postCommit.Register(async token =>
        {
            await _cache.RemoveAsync(CacheKeys.Detail(request.Id), token);
            foreach (var approverId in affectedUserIds)
            {
                await _cache.IncrementAsync(CacheKeys.PendingVersion(approverId), token);
            }
        });

        return new LeaveOperationResultDto(
            leave.Id,
            leave.LeaveNo,
            leave.Status.ToString(),
            finished ? "审批完成，请假单已全部通过" : "审批通过，已流转到下一节点");
    }

    private static EmailOutboxMessage BuildOutbox(
        string messageType, string leaveNo, string recipient, string subject, string body, DateTime now)
        => new()
        {
            MessageType = messageType,
            BusinessKey = leaveNo,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = OutboxStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        };

    private async Task<string> GetApplicantNameAsync(long applicantId, CancellationToken cancellationToken)
    {
        var name = await _users.Query().AsNoTracking()
            .Where(u => u.Id == applicantId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        return name ?? "申请人";
    }

    private async Task<List<User>> LoadActiveUsersAsync(IEnumerable<long> ids, CancellationToken cancellationToken)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new List<User>();
        }

        return await _users.Query()
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id) && u.IsActive)
            .ToListAsync(cancellationToken);
    }
}
