namespace LeaveApproval.Application.Common.Interfaces;

/// <summary>当前用户上下文（由 API 层 Filter 从认证后的 Claims 解析填充）。</summary>
public interface ICurrentUserService
{
    long? UserId { get; }

    string? UserName { get; }

    string? Role { get; }

    void Populate(long userId, string userName, string role);
}
