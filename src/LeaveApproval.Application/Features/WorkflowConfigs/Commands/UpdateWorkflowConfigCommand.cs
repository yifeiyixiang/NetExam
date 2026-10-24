using FluentValidation;
using LeaveApproval.Application.Common.Markers;
using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.WorkflowConfigs.Commands;

/// <summary>修改流程配置（仅 Admin）。已被单据使用的流程节点结构锁定，节点修改需新建流程版本。</summary>
public class UpdateWorkflowConfigCommand : IRequest<WorkflowConfigDto>, ITransactionalRequest
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>传 null 表示不修改节点结构。</summary>
    public List<WorkflowNodeInputDto>? Nodes { get; set; }
}

public class UpdateWorkflowConfigCommandValidator : AbstractValidator<UpdateWorkflowConfigCommand>
{
    public UpdateWorkflowConfigCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("流程配置 ID 不合法");

        RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("流程名称长度不能超过 100")
            .When(x => x.Name != null);

        RuleFor(x => x)
            .Must(x => x.Nodes == null || x.Nodes.Select(n => n.NodeOrder).Distinct().Count() == x.Nodes.Count)
            .WithMessage("节点顺序不能重复")
            .When(x => x.Nodes is { Count: > 0 })
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
        }).When(x => x.Nodes != null);
    }
}
