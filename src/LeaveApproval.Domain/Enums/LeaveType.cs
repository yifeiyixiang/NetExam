namespace LeaveApproval.Domain.Enums;

/// <summary>请假类型（数据库以字符串存储）</summary>
public enum LeaveType
{
    Annual = 1,      // 年假
    Sick = 2,        // 病假
    Personal = 3,    // 事假
    Marriage = 4,    // 婚假
    Bereavement = 5, // 丧假
    Compensatory = 6,// 调休
    Other = 99       // 其他
}
