using FluentAssertions;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using LeaveApproval.Domain.Services;
using Xunit;

namespace LeaveApproval.UnitTests;

/// <summary>流程配置启用校验规则。</summary>
public class WorkflowConfigPolicyTests
{
    private static WorkflowNode Node(int order, string name, ApproverType type, long? approverId = null)
        => new() { NodeOrder = order, NodeName = name, ApproverType = type, ApproverUserId = approverId, IsRequired = true };

    [Fact]
    public void 启用_缺少任何节点_校验不通过()
    {
        var errors = WorkflowConfigPolicy.ValidateForActivation(Array.Empty<WorkflowNode>());

        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("至少"));
    }

    [Fact]
    public void 启用_指定用户节点缺少审批人_校验不通过()
    {
        var nodes = new[] { Node(1, "经理审批", ApproverType.SpecificUser, approverId: null) };

        var errors = WorkflowConfigPolicy.ValidateForActivation(nodes);

        errors.Should().Contain(e => e.Contains("未配置审批人"));
    }

    [Fact]
    public void 启用_节点顺序重复_校验不通过()
    {
        var nodes = new[]
        {
            Node(1, "直属领导审批", ApproverType.DirectLeader),
            Node(1, "部门经理审批", ApproverType.DepartmentManager)
        };

        var errors = WorkflowConfigPolicy.ValidateForActivation(nodes);

        errors.Should().Contain(e => e.Contains("顺序不能重复"));
    }

    [Fact]
    public void 启用_节点顺序非正数_校验不通过()
    {
        var nodes = new[] { Node(0, "审批节点", ApproverType.DirectLeader) };

        var errors = WorkflowConfigPolicy.ValidateForActivation(nodes);

        errors.Should().Contain(e => e.Contains("正整数"));
    }

    [Fact]
    public void 启用_两个有效节点_校验通过()
    {
        var nodes = new[]
        {
            Node(1, "直属领导审批", ApproverType.DirectLeader),
            Node(2, "部门经理审批", ApproverType.DepartmentManager)
        };

        var errors = WorkflowConfigPolicy.ValidateForActivation(nodes);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void 启用_指定用户且配置了存在的审批人_校验通过()
    {
        var nodes = new[] { Node(1, "经理审批", ApproverType.SpecificUser, approverId: 300) };

        var errors = WorkflowConfigPolicy.ValidateForActivation(nodes);

        errors.Should().BeEmpty();
    }
}
