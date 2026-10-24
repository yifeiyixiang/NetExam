namespace LeaveApproval.Domain.Common;

/// <summary>业务单号生成器：LR + yyyyMMddHHmmss + 4 位随机数，依赖 LeaveNo 唯一索引兜底重试。</summary>
public static class LeaveNumberGenerator
{
    public static string Generate(DateTime utcNow)
    {
        var random = Random.Shared.Next(0, 10000);
        return $"LR{utcNow:yyyyMMddHHmmss}{random:D4}";
    }
}
