namespace LeaveApproval.Domain.Common;

/// <summary>实现该接口的实体使用整型乐观并发令牌，由 AppDbContext 在 SaveChanges 时自增。</summary>
public interface IHasConcurrencyVersion
{
    int Version { get; set; }
}
