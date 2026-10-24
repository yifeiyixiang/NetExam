using LeaveApproval.Domain.Enums;

namespace LeaveApproval.Domain.Entities;

/// <summary>审批流程配置（模板）。已启用且被单据使用后，节点结构不可修改（见应用层校验）。</summary>
public class WorkflowConfig
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>适用部门，null 表示全局适用。同一适用范围仅允许一个启用中的配置。</summary>
    public long? DepartmentId { get; set; }

    /// <summary>节点结构变更时自增。</summary>
    public int Version { get; set; } = 1;

    public bool IsActive { get; set; }

    public long? CreatedByUserId { get; set; }

    public long? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Department? Department { get; set; }

    public User? CreatedBy { get; set; }

    public User? UpdatedBy { get; set; }

    public ICollection<WorkflowNode> Nodes { get; set; } = new List<WorkflowNode>();
}

/// <summary>流程节点。定义节点顺序与审批人规则，实际审批人在提交请假单时快照到审批任务。</summary>
public class WorkflowNode
{
    public long Id { get; set; }

    public long WorkflowConfigId { get; set; }

    /// <summary>节点顺序，从 1 开始，同一配置内不可重复。</summary>
    public int NodeOrder { get; set; }

    public string NodeName { get; set; } = null!;

    public ApproverType ApproverType { get; set; }

    /// <summary>ApproverType=SpecificUser 时必填。</summary>
    public long? ApproverUserId { get; set; }

    public bool IsRequired { get; set; } = true;

    public WorkflowConfig? WorkflowConfig { get; set; }

    public User? ApproverUser { get; set; }
}
