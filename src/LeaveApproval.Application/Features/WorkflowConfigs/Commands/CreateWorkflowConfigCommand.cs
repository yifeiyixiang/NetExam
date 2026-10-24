using FluentValidation;
using LeaveApproval.Application.Common.Markers;
using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.WorkflowConfigs.Commands;

/// <summary>创建流程配置（仅 Admin）。</summary>
public class CreateWorkflowConfigCommand : IRequest<WorkflowConfigDto>, ITransactionalRequest
{
    public string Name { get; set; } = null!;

    /// <summary>适用部门，null 表示全局适用。</summary>
    public long? DepartmentId { get; set; }

    public bool IsActive { get; set; }

    public List<WorkflowNodeInputDto> Nodes { get; set; } = new();
}

public class CreateWorkflowConfigCommandValidator : AbstractValidator<CreateWorkflowConfigCommand>
{
    public CreateWorkflowConfigCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("流程名称不能为空")
            .MaximumLength(100).WithMessage("流程名称长度不能超过 100");

        RuleFor(x => x.Nodes)
            .NotEmpty().WithMessage("流程配置至少包含一个有效节点");

        RuleFor(x => x)
            .Must(x => x.Nodes.Select(n => n.NodeOrder).Distinct().Count() == x.Nodes.Count)
            .WithMessage("节点顺序不能重复")
            .When(x => x.Nodes.Count > 0)
            .WithName("Nodes");

        RuleForEach(x => x.Nodes).ChildRules(node =>
        {
            node.RuleFor(n => n.NodeOrder)
                .GreaterThan(0).WithMessage("节点顺序必须为正整数");

            node.RuleFor(n => n.NodeName)
                .NotEmpty().WithMessage("节点名称不能为空")
                .MaximumLength(50).WithMessage("节点名称长度不能超过 50");

            node.RuleFor(n => n.ApproverType)
                .IsInEnum().WithMessage("节点审批人类型不合法");

            node.RuleFor(n => n.ApproverUserId)
                .NotNull().GreaterThan(0).WithMessage("指定用户审批的节点必须配置审批人")
                .When(n => n.ApproverType == Domain.Enums.ApproverType.SpecificUser);
        });
    }
}
