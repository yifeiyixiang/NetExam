namespace LeaveApproval.Domain.Enums;

/// <summary>
/// 节点审批人类型：
/// SpecificUser 绑定具体用户；DirectLeader 解析为申请人的直属领导；
/// DepartmentManager 解析为部门经理（Department.ManagerUserId）。
/// 提交请假单时会把解析结果冻结为审批任务快照。
/// </summary>
public enum ApproverType
{
    SpecificUser = 1,
    DirectLeader = 2,
    DepartmentManager = 3
}
