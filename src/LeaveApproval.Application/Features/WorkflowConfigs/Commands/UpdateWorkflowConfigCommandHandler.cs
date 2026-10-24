using AutoMapper;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using LeaveApproval.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LeaveApproval.Application.Features.WorkflowConfigs.Commands;

public class UpdateWorkflowConfigCommandHandler : IRequestHandler<UpdateWorkflowConfigCommand, WorkflowConfigDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBaseRepository<WorkflowConfig> _configs;
    private readonly IBaseRepository<LeaveRequest> _leaveRequests;
    private readonly IBaseRepository<User> _users;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public UpdateWorkflowConfigCommandHandler(
        IUnitOfWork unitOfWork,
        IBaseRepository<WorkflowConfig> configs,
        IBaseRepository<LeaveRequest> leaveRequests,
        IBaseRepository<User> users,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _configs = configs;
        _leaveRequests = leaveRequests;
        _users = users;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<WorkflowConfigDto> Handle(UpdateWorkflowConfigCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new Common.Exceptions.AuthenticationFailedException("未获取到当前用户身份");
        var now = DateTime.UtcNow;

        var config = await _configs.Query()
            .Include(c => c.Nodes)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("流程配置", request.Id);

        // 已有单据使用的流程版本不得被追溯修改：节点结构锁定
        var inUse = await _leaveRequests.ExistsAsync(l => l.WorkflowConfigId == config.Id, cancellationToken);

        if (request.Nodes != null)
        {
            if (inUse)
            {
                throw new ConflictException("该流程已被请假单使用，节点结构不可修改；请新建流程配置作为新版本使用");
            }

            await EnsureSpecificApproversExistAsync(request.Nodes, cancellationToken);

            config.Nodes.Clear();
            foreach (var node in request.Nodes)
            {
                config.Nodes.Add(new WorkflowNode
                {
                    NodeOrder = node.NodeOrder,
                    NodeName = node.NodeName.Trim(),
                    ApproverType = node.ApproverType,
                    ApproverUserId = node.ApproverType == ApproverType.SpecificUser ? node.ApproverUserId : null,
                    IsRequired = node.IsRequired
                });
            }

            config.Version++;
        }

        if (request.Name != null)
        {
            config.Name = request.Name.Trim();
        }

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value)
            {
                var errors = WorkflowConfigPolicy.ValidateForActivation(config.Nodes);
                if (errors.Count > 0)
                {
                    throw new ConflictException($"流程配置无效，无法启用：{string.Join("；", errors)}");
                }

                if (await _configs.Query().AsNoTracking()
                        .AnyAsync(c => c.IsActive && c.Id != config.Id && c.DepartmentId == config.DepartmentId,
                            cancellationToken))
                {
                    throw new ConflictException("同一适用范围已存在启用中的流程配置，请先停用原配置");
                }
            }

            config.IsActive = request.IsActive.Value;
        }

        config.UpdatedByUserId = userId;
        config.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _configs.Query()
            .AsNoTracking()
            .Include(c => c.Department)
            .Include(c => c.Nodes).ThenInclude(n => n.ApproverUser)
            .FirstAsync(c => c.Id == config.Id, cancellationToken);

        return _mapper.Map<WorkflowConfigDto>(saved);
    }

    private async Task EnsureSpecificApproversExistAsync(
        IEnumerable<WorkflowNodeInputDto> nodes, CancellationToken cancellationToken)
    {
        var approverIds = nodes
            .Where(n => n.ApproverType == ApproverType.SpecificUser && n.ApproverUserId != null)
            .Select(n => n.ApproverUserId!.Value)
            .Distinct()
            .ToList();

        if (approverIds.Count == 0)
        {
            return;
        }

        var existingIds = await _users.Query().AsNoTracking()
            .Where(u => approverIds.Contains(u.Id) && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var missing = approverIds.Except(existingIds).ToList();
        if (missing.Count > 0)
        {
            throw new ConflictException($"节点审批人不存在或已停用：{string.Join("、", missing)}");
        }
    }
}
