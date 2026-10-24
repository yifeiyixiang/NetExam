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

public class CreateWorkflowConfigCommandHandler : IRequestHandler<CreateWorkflowConfigCommand, WorkflowConfigDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBaseRepository<WorkflowConfig> _configs;
    private readonly IBaseRepository<User> _users;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public CreateWorkflowConfigCommandHandler(
        IUnitOfWork unitOfWork,
        IBaseRepository<WorkflowConfig> configs,
        IBaseRepository<User> users,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _configs = configs;
        _users = users;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<WorkflowConfigDto> Handle(CreateWorkflowConfigCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new Common.Exceptions.AuthenticationFailedException("未获取到当前用户身份");
        var now = DateTime.UtcNow;

        // 指定用户审批的节点：审批人必须存在且有效
        await EnsureSpecificApproversExistAsync(request.Nodes, cancellationToken);

        var config = new WorkflowConfig
        {
            Name = request.Name.Trim(),
            DepartmentId = request.DepartmentId,
            IsActive = false,
            Version = 1,
            CreatedByUserId = userId,
            CreatedAt = now
        };

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

        // 启用前必须通过校验，且同一适用范围不能存在冲突的有效流程
        if (request.IsActive)
        {
            var errors = WorkflowConfigPolicy.ValidateForActivation(config.Nodes);
            if (errors.Count > 0)
            {
                throw new ConflictException($"流程配置无效，无法启用：{string.Join("；", errors)}");
            }

            if (await HasConflictingActiveConfigAsync(config.DepartmentId, excludeId: null, cancellationToken))
            {
                throw new ConflictException("同一适用范围已存在启用中的流程配置，请先停用原配置");
            }

            config.IsActive = true;
        }

        await _configs.AddAsync(config);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 重新加载审批人导航用于展示
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

    private async Task<bool> HasConflictingActiveConfigAsync(
        long? departmentId, long? excludeId, CancellationToken cancellationToken)
    {
        return await _configs.Query().AsNoTracking()
            .AnyAsync(c => c.IsActive &&
                           c.DepartmentId == departmentId &&
                           (excludeId == null || c.Id != excludeId),
                cancellationToken);
    }
}
