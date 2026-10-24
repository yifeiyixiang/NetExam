using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.LeaveRequests.Queries;

/// <summary>请假单详情（优先读取 Redis 缓存）。</summary>
public record GetLeaveRequestDetailQuery(long Id) : IRequest<LeaveRequestDetailDto>;
