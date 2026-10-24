namespace LeaveApproval.Application.Common.Markers;

/// <summary>实现该标记接口的请求将由 TransactionBehavior 包裹在数据库事务中执行；查询请求不要实现。</summary>
public interface ITransactionalRequest
{
}

/// <summary>响应携带业务单号，供日志管道补充记录 LeaveNo（单据编号可能在 Handler 内才生成）。</summary>
public interface IHaveLeaveNo
{
    string LeaveNo { get; }
}
