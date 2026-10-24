using FluentValidation;
using LeaveApproval.Application.Common.Models;
using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.LeaveRequests.Queries;

/// <summary>我的请假申请列表（仅当前登录用户自己的单据）。</summary>
public record GetMyLeaveRequestsQuery(int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<LeaveRequestListItemDto>>;

public class GetMyLeaveRequestsQueryValidator : AbstractValidator<GetMyLeaveRequestsQuery>
{
    public GetMyLeaveRequestsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页大小必须在 1 到 100 之间");
    }
}
