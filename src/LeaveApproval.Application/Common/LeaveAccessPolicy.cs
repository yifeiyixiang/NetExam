using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;

namespace LeaveApproval.Application.Common;

/// <summary>请假单可见性策略：申请人本人、该单据的审批任务审批人、Admin 可查看详情与附件。</summary>
public static class LeaveAccessPolicy
{
    public static void EnsureCanView(long applicantId, IEnumerable<long> taskApproverIds, ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException("未获取到当前用户身份");

        if (string.Equals(currentUser.Role, Domain.Common.Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (userId == applicantId)
        {
            return;
        }

        if (taskApproverIds.Contains(userId))
        {
            return;
        }

        throw new ForbiddenAccessException("您无权查看该请假单");
    }
}
