namespace LeaveApproval.Application.DTOs;

public class WorkflowNodeDto
{
    public long Id { get; set; }

    public int NodeOrder { get; set; }

    public string NodeName { get; set; } = null!;

    public string ApproverType { get; set; } = null!;

    public long? ApproverUserId { get; set; }

    public string? ApproverUserName { get; set; }

    public bool IsRequired { get; set; }
}

public class WorkflowConfigDto
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public long? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public int Version { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyList<WorkflowNodeDto> Nodes { get; set; } = Array.Empty<WorkflowNodeDto>();
}

/// <summary>节点输入（创建/修改流程配置）。</summary>
public class WorkflowNodeInputDto
{
    public int NodeOrder { get; set; }

    public string NodeName { get; set; } = null!;

    /// <summary>1=指定用户 2=直属领导 3=部门经理。</summary>
    public Domain.Enums.ApproverType ApproverType { get; set; }

    public long? ApproverUserId { get; set; }

    public bool IsRequired { get; set; } = true;
}
