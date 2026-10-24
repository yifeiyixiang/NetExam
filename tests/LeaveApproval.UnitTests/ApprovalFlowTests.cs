using FluentAssertions;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using LeaveApproval.Domain.Exceptions;
using LeaveApproval.Domain.Services;
using Xunit;

namespace LeaveApproval.UnitTests;

/// <summary>审批流转核心规则的单元测试（纯领域逻辑，不依赖数据库）。</summary>
public class ApprovalFlowTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>构造一张两节点请假单：节点1 有两个审批人（任一通过即流转），节点2 一个审批人。</summary>
    private static (LeaveRequest Leave, LeaveApprovalTask Node1A, LeaveApprovalTask Node1B, LeaveApprovalTask Node2) CreatePendingLeave()
    {
        var leave = new LeaveRequest
        {
            Id = 1,
            LeaveNo = "LRTEST0001",
            Status = LeaveStatus.PendingApproval,
            CurrentNodeId = 11,
            CurrentNodeOrder = 1
        };

        var node1a = new LeaveApprovalTask { Id = 101, WorkflowNodeId = 11, NodeOrder = 1, NodeName = "直属领导审批", ApproverUserId = 200, Status = ApprovalTaskStatus.Pending };
        var node1b = new LeaveApprovalTask { Id = 102, WorkflowNodeId = 11, NodeOrder = 1, NodeName = "直属领导审批", ApproverUserId = 201, Status = ApprovalTaskStatus.Pending };
        var node2 = new LeaveApprovalTask { Id = 103, WorkflowNodeId = 12, NodeOrder = 2, NodeName = "部门经理审批", ApproverUserId = 300, Status = ApprovalTaskStatus.Upcoming };

        leave.Tasks.Add(node1a);
        leave.Tasks.Add(node1b);
        leave.Tasks.Add(node2);

        return (leave, node1a, node1b, node2);
    }

    [Fact]
    public void Approve_当前节点审批人通过_应激活下一节点()
    {
        var (leave, node1a, node1b, node2) = CreatePendingLeave();

        var finished = ApprovalFlow.Approve(leave, node1a, 200, Now);

        finished.Should().BeFalse();
        leave.Status.Should().Be(LeaveStatus.PendingApproval);
        node1a.Status.Should().Be(ApprovalTaskStatus.Approved);
        node1b.Status.Should().Be(ApprovalTaskStatus.Skipped); // 节点内其余任务同步失效
        node2.Status.Should().Be(ApprovalTaskStatus.Pending);  // 下一节点被激活
        leave.CurrentNodeOrder.Should().Be(2);
        leave.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Approve_最后一节点通过_单据应变为Approved()
    {
        var (leave, _, _, node2) = CreatePendingLeave();
        // 预置：节点1 已通过、节点2 已激活为 Pending
        leave.Tasks.First(t => t.Id == 101).Status = ApprovalTaskStatus.Approved;
        leave.Tasks.First(t => t.Id == 102).Status = ApprovalTaskStatus.Skipped;
        node2.Status = ApprovalTaskStatus.Pending;
        leave.CurrentNodeId = 12;
        leave.CurrentNodeOrder = 2;

        var finished = ApprovalFlow.Approve(leave, node2, 300, Now);

        finished.Should().BeTrue();
        leave.Status.Should().Be(LeaveStatus.Approved);
        leave.CompletedAt.Should().Be(Now);
        leave.CurrentNodeId.Should().BeNull();
    }

    [Fact]
    public void Approve_任务已被处理_再次审批应抛出冲突异常()
    {
        var (leave, node1a, _, _) = CreatePendingLeave();
        ApprovalFlow.Approve(leave, node1a, 200, Now);

        var act = () => ApprovalFlow.Approve(leave, node1a, 200, Now.AddMinutes(1));

        act.Should().Throw<TaskAlreadyProcessedException>();
    }

    [Fact]
    public void Approve_非审批人操作_应抛出权限异常()
    {
        var (leave, node1a, _, _) = CreatePendingLeave();

        var act = () => ApprovalFlow.Approve(leave, node1a, 999, Now);

        act.Should().Throw<ApprovalPermissionException>();
    }

    [Fact]
    public void Approve_下一节点审批人提前操作_越级应被拒绝()
    {
        var (leave, _, _, node2) = CreatePendingLeave();

        // node2 审批人在节点1 未完成时尝试审批（任务尚未激活）
        var act = () => ApprovalFlow.Approve(leave, node2, 300, Now);

        act.Should().Throw<ApprovalPermissionException>();
    }

    [Fact]
    public void Reject_审批人驳回_单据应变Rejected且后续任务全部作废()
    {
        var (leave, node1a, node1b, node2) = CreatePendingLeave();

        ApprovalFlow.Reject(leave, node1a, 200, Now);

        leave.Status.Should().Be(LeaveStatus.Rejected);
        leave.CompletedAt.Should().Be(Now);
        node1a.Status.Should().Be(ApprovalTaskStatus.Rejected);
        node1b.Status.Should().Be(ApprovalTaskStatus.Skipped);
        node2.Status.Should().Be(ApprovalTaskStatus.Skipped); // 不得继续流转
    }

    [Fact]
    public void Reject_非审批人操作_应抛出权限异常()
    {
        var (leave, node1a, _, _) = CreatePendingLeave();

        var act = () => ApprovalFlow.Reject(leave, node1a, 999, Now);

        act.Should().Throw<ApprovalPermissionException>();
    }

    [Fact]
    public void Approve_单据已终态_应抛出状态异常()
    {
        var (leave, node1a, _, _) = CreatePendingLeave();
        leave.Status = LeaveStatus.Approved;

        var act = () => ApprovalFlow.Approve(leave, node1a, 200, Now);

        act.Should().Throw<InvalidLeaveStateException>();
    }
}
