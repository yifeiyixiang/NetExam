using FluentValidation;
using LeaveApproval.Application.Common.Models;
using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.Approvals.Queries;

/// <summary>我的待审批列表。审批人 ID 取自当前登录用户，禁止信任客户端传入。</summary>
public record GetMyPendingApprovalsQuery(int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<PendingApprovalItemDto>>;

public class GetMyPendingApprovalsQueryValidator : AbstractValidator<GetMyPendingApprovalsQuery>
{
    public GetMyPendingApprovalsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页大小必须在 1 到 100 之间");
    }
}
