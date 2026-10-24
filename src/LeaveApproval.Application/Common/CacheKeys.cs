namespace LeaveApproval.Application.Common;

/// <summary>
/// 缓存键统一管理。
/// 待办列表采用"版本号失效"策略：每个审批人维护一个版本键，流转时原子自增，
/// 旧分页键自然淘汰，避免 SCAN/KEYS 扫描线上键。
/// </summary>
public static class CacheKeys
{
    private const string DetailPrefix = "leave:detail:";
    private const string PendingVersionPrefix = "leave:pending:ver:";
    private const string PendingListPrefix = "leave:pending:";

    public static string Detail(long leaveRequestId) => $"{DetailPrefix}{leaveRequestId}";

    public static string PendingVersion(long userId) => $"{PendingVersionPrefix}{userId}";

    public static string PendingList(long userId, long version, int page, int pageSize)
        => $"{PendingListPrefix}{userId}:v{version}:p{page}:s{pageSize}";
}
