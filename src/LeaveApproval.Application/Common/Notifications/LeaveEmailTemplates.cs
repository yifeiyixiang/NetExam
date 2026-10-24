namespace LeaveApproval.Application.Common.Notifications;

/// <summary>请假审批相关邮件模板（模拟发送，正文为纯文本）。</summary>
public static class LeaveEmailTemplates
{
    public static (string Subject, string Body) LeaveSubmitted(
        string leaveNo, string applicantName, string approverName, string leaveTypeName,
        DateTime startTime, DateTime endTime, decimal durationHours, string reason)
    {
        var subject = $"【待审批】{applicantName} 提交了请假申请（{leaveNo}）";
        var body = $"""
            {approverName}，您好：

            {applicantName} 提交了请假申请，等待您审批。
              单号：{leaveNo}
              类型：{leaveTypeName}
              时间：{startTime:yyyy-MM-dd HH:mm} ~ {endTime:yyyy-MM-dd HH:mm}（共 {durationHours} 小时）
              事由：{reason}

            请登录请假审批系统处理。
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) LeaveApprovedNextNode(
        string leaveNo, string applicantName, string approverName, string nodeName)
    {
        var subject = $"【待审批】{leaveNo} 已通过上一节点，等待您审批";
        var body = $"""
            {approverName}，您好：

            {applicantName} 的请假申请（{leaveNo}）已通过「{nodeName}」审批，现已流转到您所在的节点，等待您处理。

            请登录请假审批系统处理。
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) LeaveCompleted(
        string leaveNo, string applicantName)
    {
        var subject = $"【审批完成】您的请假申请已全部通过（{leaveNo}）";
        var body = $"""
            {applicantName}，您好：

            您的请假申请（{leaveNo}）已完成全部审批节点，状态：已通过。

            请登录请假审批系统查看详情。
            """;
        return (subject, body);
    }

    public static (string Subject, string Body) LeaveRejected(
        string leaveNo, string applicantName, string nodeName, string comment)
    {
        var subject = $"【已驳回】您的请假申请被驳回（{leaveNo}）";
        var body = $"""
            {applicantName}，您好：

            您的请假申请（{leaveNo}）在「{nodeName}」节点被驳回。
            驳回意见：{comment}

            请登录请假审批系统查看详情。
            """;
        return (subject, body);
    }
}
