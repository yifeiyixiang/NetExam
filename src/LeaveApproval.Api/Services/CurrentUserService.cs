using LeaveApproval.Application.Common.Interfaces;

namespace LeaveApproval.Api.Services;

/// <summary>基于 HttpContext Claims 的当前用户实现（由 CurrentUserFilter 填充）。</summary>
public class CurrentUserService : ICurrentUserService
{
    public long? UserId { get; private set; }

    public string? UserName { get; private set; }

    public string? Role { get; private set; }

    public void Populate(long userId, string userName, string role)
    {
        UserId = userId;
        UserName = userName;
        Role = role;
    }
}
