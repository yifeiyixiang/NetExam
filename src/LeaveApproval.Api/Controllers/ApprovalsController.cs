using LeaveApproval.Application.Common.Models;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Application.Features.Approvals.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveApproval.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/approvals")]
public class ApprovalsController : ControllerBase
{
    private readonly ISender _sender;

    public ApprovalsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>我的待审批列表（分页，Redis 缓存优先）。审批人 ID 取自当前登录用户。</summary>
    [HttpGet("pending")]
    public async Task<ActionResult<PagedResult<PendingApprovalItemDto>>> GetPending(
        [FromQuery] GetMyPendingApprovalsQuery query, CancellationToken cancellationToken)
    {
        return await _sender.Send(query, cancellationToken);
    }
}
