namespace LeaveApproval.Application.Common.Exceptions;

/// <summary>资源不存在：404。</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName}不存在（{key}）")
    {
    }

    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>无权访问：403。</summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message)
    {
    }
}

/// <summary>登录失败/身份缺失：401。</summary>
public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message) : base(message)
    {
    }
}

/// <summary>业务状态冲突（重复审批、流程冲突、唯一键冲突等）：409。</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
