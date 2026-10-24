using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.WorkflowConfigs.Queries;

/// <summary>流程配置详情（仅 Admin）。</summary>
public record GetWorkflowConfigQuery(long Id) : IRequest<WorkflowConfigDto>;

/// <summary>流程配置列表（仅 Admin）。</summary>
public record GetWorkflowConfigListQuery : IRequest<List<WorkflowConfigDto>>;
