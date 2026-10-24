using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;

namespace LeaveApproval.Domain.Services;

/// <summary>流程配置启用前的结构校验规则（纯逻辑，审批人是否存在由应用层结合数据库校验）。</summary>
public static class WorkflowConfigPolicy
{
    /// <returns>校验错误列表，为空表示通过。</returns>
    public static List<string> ValidateForActivation(IEnumerable<WorkflowNode> nodes)
    {
        var errors = new List<string>();
        var nodeList = nodes.ToList();

        if (nodeList.Count == 0)
        {
            errors.Add("流程至少需要配置一个审批节点");
            return errors;
        }

        if (nodeList.Select(n => n.NodeOrder).Distinct().Count() != nodeList.Count)
        {
            errors.Add("节点顺序不能重复");
        }

        foreach (var node in nodeList)
        {
            if (node.NodeOrder <= 0)
            {
                errors.Add($"节点[{node.NodeName}]的顺序必须为正整数");
            }

            if (string.IsNullOrWhiteSpace(node.NodeName))
            {
                errors.Add($"节点顺序 {node.NodeOrder} 缺少节点名称");
            }

            if (!Enum.IsDefined(node.ApproverType))
            {
                errors.Add($"节点[{node.NodeName}]的审批人类型不合法");
                continue;
            }

            if (node.ApproverType == ApproverType.SpecificUser &&
                (node.ApproverUserId is null or <= 0))
            {
                errors.Add($"节点[{node.NodeName}]指定了具体审批人类型，但未配置审批人");
            }
        }

        return errors;
    }
}
