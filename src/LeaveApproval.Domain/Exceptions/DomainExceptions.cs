namespace LeaveApproval.Domain.Exceptions;

/// <summary>领域规则异常基类，由 API 全局异常中间件映射为 403/409 等 HTTP 状态码。</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

/// <summary>当前用户不是任务审批人（或未流转到该用户所在节点）：403。</summary>
public class ApprovalPermissionException : DomainException
{
    public ApprovalPermissionException(string message) : base(message)
    {
    }
}

/// <summary>审批任务已被处理（重复审批/重复驳回）：409。</summary>
public class TaskAlreadyProcessedException : DomainException
{
    public TaskAlreadyProcessedException(string message) : base(message)
    {
    }
}

/// <summary>请假单当前状态不允许该操作（状态冲突）：409。</summary>
public class InvalidLeaveStateException : DomainException
{
    public InvalidLeaveStateException(string message) : base(message)
    {
    }
}
