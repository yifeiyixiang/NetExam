using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using LeaveApproval.Domain.Exceptions;

namespace LeaveApproval.Domain.Services;

/// <summary>
/// 审批流转核心规则（纯领域逻辑，不依赖基础设施）：
/// - 仅 PendingApproval 单据、Pending 任务可被其审批人操作；
/// - 同意时节点内其余任务置为 Skipped（任一审批人通过即流转），并激活下一节点任务；
/// - 驳回时单据直接终止，其余未处理任务全部 Skipped；
/// - Status 并发令牌配合条件更新，保证并发审批同一任务只有一个成功。
/// </summary>
public static class ApprovalFlow
{
    /// <returns>true 表示流程已完成（单据变为 Approved），false 表示流转到下一节点。</returns>
    public static bool Approve(LeaveRequest leave, LeaveApprovalTask task, long operatorId, DateTime now)
    {
        EnsureCanOperate(leave, task, operatorId);

        task.Status = ApprovalTaskStatus.Approved;
        task.CompletedAt = now;

        // 节点内其他待办任务作废：默认策略为节点内任一指定审批人完成即可流转
        foreach (var sibling in leave.Tasks.Where(t =>
                     t.Id != task.Id &&
                     t.WorkflowNodeId == task.WorkflowNodeId &&
                     t.Status == ApprovalTaskStatus.Pending))
        {
            sibling.Status = ApprovalTaskStatus.Skipped;
            sibling.CompletedAt = now;
        }

        var nextTasks = leave.Tasks
            .Where(t => t.NodeOrder == task.NodeOrder + 1)
            .OrderBy(t => t.Id)
            .ToList();

        if (nextTasks.Count > 0)
        {
            foreach (var next in nextTasks.Where(t => t.Status == ApprovalTaskStatus.Upcoming))
            {
                next.Status = ApprovalTaskStatus.Pending;
            }

            leave.CurrentNodeId = nextTasks[0].WorkflowNodeId;
            leave.CurrentNodeOrder = nextTasks[0].NodeOrder;
            return false;
        }

        leave.Status = LeaveStatus.Approved;
        leave.CompletedAt = now;
        leave.CurrentNodeId = null;
        leave.CurrentNodeOrder = null;
        return true;
    }

    public static void Reject(LeaveRequest leave, LeaveApprovalTask task, long operatorId, DateTime now)
    {
        EnsureCanOperate(leave, task, operatorId);

        task.Status = ApprovalTaskStatus.Rejected;
        task.CompletedAt = now;

        // 其余待办与尚未激活的任务全部作废，流程终止，不得继续流转
        foreach (var other in leave.Tasks.Where(t =>
                     t.Id != task.Id &&
                     t.Status is ApprovalTaskStatus.Pending or ApprovalTaskStatus.Upcoming))
        {
            other.Status = ApprovalTaskStatus.Skipped;
            other.CompletedAt = now;
        }

        leave.Status = LeaveStatus.Rejected;
        leave.CompletedAt = now;
        leave.CurrentNodeId = null;
        leave.CurrentNodeOrder = null;
    }

    private static void EnsureCanOperate(LeaveRequest leave, LeaveApprovalTask task, long operatorId)
    {
        if (leave.Status != LeaveStatus.PendingApproval)
        {
            throw new InvalidLeaveStateException($"请假单当前状态为 {leave.Status}，不允许执行审批操作");
        }

        if (task.ApproverUserId != operatorId)
        {
            throw new ApprovalPermissionException("您不是该审批任务的审批人，无权操作");
        }

        if (task.Status == ApprovalTaskStatus.Upcoming)
        {
            throw new ApprovalPermissionException("该审批任务尚未流转到您所在节点，无法越级审批");
        }

        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new TaskAlreadyProcessedException("该审批任务已被处理，请勿重复操作");
        }
    }
}
