using LeaveApproval.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace LeaveApproval.Api.Filters;

/// <summary>
/// 用户身份解析 Filter：认证成功后从 Claims 解析用户 ID、用户名、角色并填充 ICurrentUserService。
/// 不负责 Token 签名验证（由 JWT Bearer 中间件完成）。
/// </summary>
public class CurrentUserFilter : IAsyncActionFilter
{
    private readonly ICurrentUserService _currentUser;

    public CurrentUserFilter(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(sub) || !long.TryParse(sub, out var userId))
            {
                throw new LeaveApproval.Application.Common.Exceptions.AuthenticationFailedException("Token 中缺少有效的用户标识");
            }

            var userName = user.FindFirstValue("name") ?? string.Empty;
            var role = user.FindFirstValue("role") ?? string.Empty;

            _currentUser.Populate(userId, userName, role);
        }

        return next();
    }
}
