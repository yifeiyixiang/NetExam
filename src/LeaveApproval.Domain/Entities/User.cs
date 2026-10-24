namespace LeaveApproval.Domain.Entities;

public class User
{
    public long Id { get; set; }

    /// <summary>登录名，唯一。</summary>
    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public long? DepartmentId { get; set; }

    /// <summary>直属领导（节点 ApproverType=DirectLeader 时解析为该用户）。</summary>
    public long? DirectManagerId { get; set; }

    /// <summary>角色：Employee / Manager / Admin（见 Roles）。</summary>
    public string Role { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Department? Department { get; set; }

    public User? DirectManager { get; set; }
}

public class Department
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>部门经理（节点 ApproverType=DepartmentManager 时解析为该用户）。</summary>
    public long? ManagerUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? Manager { get; set; }
}
