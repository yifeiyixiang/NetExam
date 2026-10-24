using LeaveApproval.Application.Common;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.Common.Notifications;
using LeaveApproval.Domain.Common;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Application.Features.LeaveRequests.Commands;

public class SubmitLeaveRequestCommandHandler : IRequestHandler<SubmitLeaveRequestCommand, SubmitLeaveResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBaseRepository<User> _users;
    private readonly IBaseRepository<WorkflowConfig> _workflowConfigs;
    private readonly IBaseRepository<LeaveRequest> _leaveRequests;
    private readonly IBaseRepository<EmailOutboxMessage> _outbox;
    private readonly ICacheService _cache;
    private readonly IFileStorageService _fileStorage;
    private readonly IPostCommitExecutor _postCommit;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SubmitLeaveRequestCommandHandler> _logger;

    public SubmitLeaveRequestCommandHandler(
        IUnitOfWork unitOfWork,
        IBaseRepository<User> users,
        IBaseRepository<WorkflowConfig> workflowConfigs,
        IBaseRepository<LeaveRequest> leaveRequests,
        IBaseRepository<EmailOutboxMessage> outbox,
        ICacheService cache,
        IFileStorageService fileStorage,
        IPostCommitExecutor postCommit,
        ICurrentUserService currentUser,
        ILogger<SubmitLeaveRequestCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _users = users;
        _workflowConfigs = workflowConfigs;
        _leaveRequests = leaveRequests;
        _outbox = outbox;
        _cache = cache;
        _fileStorage = fileStorage;
        _postCommit = postCommit;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<SubmitLeaveResultDto> Handle(SubmitLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("未获取到当前用户身份");

        var applicant = await _users.Query()
            .AsNoTracking()
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new AuthenticationFailedException("当前用户不存在或已失效");

        if (!applicant.IsActive)
        {
            throw new AuthenticationFailedException("当前用户已停用，无法提交请假申请");
        }

        // 1. 获取适用的启用中流程配置（优先部门级，其次全局）
        var config = await FindActiveWorkflowConfigAsync(applicant.DepartmentId, cancellationToken)
            ?? throw new ConflictException("未找到适用于当前部门的启用中的审批流程配置，请联系管理员配置");

        // 2. 按配置解析各节点审批人（作为审批任务快照的来源）
        var nodeApprovers = await ResolveApproversAsync(config, applicant, cancellationToken);
        var firstNode = nodeApprovers[0];

        // 3. 先保存附件文件；数据库操作失败时在 catch 中补偿删除，避免孤立文件
        var savedFiles = new List<StoredFile>();
        if (request.Attachments is { Count: > 0 })
        {
            foreach (var file in request.Attachments)
            {
                await using var stream = file.OpenReadStream();
                savedFiles.Add(await _fileStorage.SaveAsync(stream, file.FileName, file.ContentType, cancellationToken));
            }
        }

        var now = DateTime.UtcNow;
        var durationHours = Math.Round((decimal)(request.EndTime - request.StartTime).TotalHours, 1, MidpointRounding.AwayFromZero);

        var leave = new LeaveRequest
        {
            ApplicantId = applicant.Id,
            DepartmentId = applicant.DepartmentId,
            LeaveType = request.LeaveType,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            DurationHours = durationHours,
            Reason = request.Reason,
            Status = LeaveStatus.PendingApproval,
            CurrentNodeId = firstNode.Node.Id,
            CurrentNodeOrder = firstNode.Node.NodeOrder,
            WorkflowConfigId = config.Id,
            WorkflowVersion = config.Version,
            SubmittedAt = now,
            CreatedAt = now
        };

        try
        {
            // 4. 生成业务单号（唯一索引兜底，冲突重试）
            leave.LeaveNo = LeaveNumberGenerator.Generate(now);
            await _leaveRequests.AddAsync(leave, cancellationToken);
            await SaveWithUniqueLeaveNoRetryAsync(leave, cancellationToken);

            // 5. 附件元数据
            foreach (var saved in savedFiles)
            {
                leave.Attachments.Add(new LeaveAttachment
                {
                    OriginalFileName = saved.OriginalFileName,
                    StorageKey = saved.StorageKey,
                    ContentType = saved.ContentType,
                    Size = saved.Size,
                    UploadedBy = userId,
                    CreatedAt = now
                });
            }

            // 6. 冻结流程快照：为全部节点创建审批任务，仅第一节点激活为 Pending
            foreach (var (node, approvers, index) in nodeApprovers)
            {
                foreach (var approver in approvers)
                {
                    leave.Tasks.Add(new LeaveApprovalTask
                    {
                        WorkflowNodeId = node.Id,
                        NodeOrder = node.NodeOrder,
                        NodeName = node.NodeName,
                        ApproverUserId = approver.Id,
                        Status = index == 0 ? ApprovalTaskStatus.Pending : ApprovalTaskStatus.Upcoming,
                        CreatedAt = now
                    });
                }
            }

            // 7. 提交操作记录
            leave.Records.Add(new ApprovalRecord
            {
                WorkflowNodeId = firstNode.Node.Id,
                OperatorId = userId,
                OperatorName = applicant.DisplayName,
                Action = ApprovalAction.Submit,
                FromStatus = null,
                ToStatus = LeaveStatus.PendingApproval.ToString(),
                CreatedAt = now
            });

            // 8. Outbox：通知邮件与业务数据同事务写入，后台进程在事务提交后发送
            foreach (var approver in nodeApprovers[0].Approvers)
            {
                var (subject, body) = LeaveEmailTemplates.LeaveSubmitted(
                    leave.LeaveNo, applicant.DisplayName, approver.DisplayName,
                    request.LeaveType.ToString(), request.StartTime, request.EndTime, durationHours, request.Reason);

                await _outbox.AddAsync(new EmailOutboxMessage
                {
                    MessageType = "LeaveSubmitted",
                    BusinessKey = leave.LeaveNo,
                    Recipient = approver.Email,
                    Subject = subject,
                    Body = body,
                    Status = OutboxStatus.Pending,
                    NextAttemptAt = now,
                    CreatedAt = now
                });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            // 事务的提交/回滚由 TransactionBehavior 负责
        }
        catch
        {
            await CleanupFilesAsync(savedFiles);
            throw;
        }

        _logger.LogInformation(
            "请假单已提交：LeaveNo={LeaveNo}，申请人={ApplicantId}，当前节点={NodeName}，附件数={AttachmentCount}",
            leave.LeaveNo, userId, firstNode.Node.NodeName, savedFiles.Count);

        // 9. 事务提交后（TransactionBehavior 触发）：失效第一节点审批人的待办缓存
        var firstApproverIds = nodeApprovers[0].Approvers.Select(u => u.Id).ToHashSet();
        _postCommit.Register(async token =>
        {
            foreach (var approverId in firstApproverIds)
            {
                await _cache.IncrementAsync(CacheKeys.PendingVersion(approverId), token);
            }
        });

        return new SubmitLeaveResultDto(
            leave.Id,
            leave.LeaveNo,
            leave.Status.ToString(),
            (int)durationHours,
            firstNode.Node.NodeName);
    }

    private async Task SaveWithUniqueLeaveNoRetryAsync(LeaveRequest leave, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (attempt < 3 && IsDuplicateKeyException(ex))
            {
                leave.LeaveNo = LeaveNumberGenerator.Generate(DateTime.UtcNow);
            }
        }
    }

    private async Task<WorkflowConfig?> FindActiveWorkflowConfigAsync(long? departmentId, CancellationToken cancellationToken)
    {
        if (departmentId != null)
        {
            var deptSpecific = await _workflowConfigs.Query()
                .AsNoTracking()
                .Include(c => c.Nodes)
                .FirstOrDefaultAsync(c => c.IsActive && c.DepartmentId == departmentId, cancellationToken);
            if (deptSpecific != null)
            {
                return deptSpecific;
            }
        }

        return await _workflowConfigs.Query()
            .AsNoTracking()
            .Include(c => c.Nodes)
            .FirstOrDefaultAsync(c => c.IsActive && c.DepartmentId == null, cancellationToken);
    }

    /// <summary>解析每个节点的审批人快照；配置无法解析时视为流程配置无效。</summary>
    private async Task<List<(WorkflowNode Node, List<User> Approvers, int Index)>> ResolveApproversAsync(
        WorkflowConfig config, User applicant, CancellationToken cancellationToken)
    {
        var nodes = config.Nodes.OrderBy(n => n.NodeOrder).ToList();
        if (nodes.Count == 0)
        {
            throw new ConflictException("流程配置未包含任何审批节点，无法提交请假申请");
        }

        var result = new List<(WorkflowNode, List<User>, int)>();

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            List<User> approvers;

            switch (node.ApproverType)
            {
                case ApproverType.DirectLeader:
                    if (applicant.DirectManagerId == null)
                    {
                        throw new ConflictException("当前用户未配置直属领导，无法提交请假申请");
                    }
                    approvers = await LoadActiveUsersAsync(new[] { applicant.DirectManagerId.Value }, cancellationToken);
                    break;

                case ApproverType.DepartmentManager:
                    if (applicant.Department?.ManagerUserId == null)
                    {
                        throw new ConflictException("当前部门未配置部门经理，无法提交请假申请");
                    }
                    approvers = await LoadActiveUsersAsync(new[] { applicant.Department.ManagerUserId.Value }, cancellationToken);
                    break;

                case ApproverType.SpecificUser:
                    if (node.ApproverUserId == null)
                    {
                        throw new ConflictException($"流程节点[{node.NodeName}]未配置审批人，无法提交请假申请");
                    }
                    approvers = await LoadActiveUsersAsync(new[] { node.ApproverUserId.Value }, cancellationToken);
                    break;

                default:
                    throw new ConflictException($"流程节点[{node.NodeName}]的审批人类型不合法");
            }

            if (approvers.Count == 0)
            {
                throw new ConflictException($"流程节点[{node.NodeName}]的审批人不存在或已停用，无法提交请假申请");
            }

            result.Add((node, approvers, index));
        }

        return result;
    }

    private async Task<List<User>> LoadActiveUsersAsync(IEnumerable<long> ids, CancellationToken cancellationToken)
    {
        var idList = ids.Distinct().ToList();
        return await _users.Query()
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id) && u.IsActive)
            .ToListAsync(cancellationToken);
    }

    private async Task CleanupFilesAsync(List<StoredFile> savedFiles)
    {
        foreach (var file in savedFiles)
        {
            try
            {
                await _fileStorage.DeleteAsync(file.StorageKey, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "补偿删除孤立文件失败：StorageKey={StorageKey}", file.StorageKey);
            }
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true ||
               ex.InnerException?.Message.Contains("1062", StringComparison.OrdinalIgnoreCase) == true;
    }
}
