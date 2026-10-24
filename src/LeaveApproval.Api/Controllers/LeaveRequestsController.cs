using LeaveApproval.Application.Common.Models;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Application.Features.LeaveRequests.Commands;
using LeaveApproval.Application.Features.LeaveRequests.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveApproval.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/leave-requests")]
public class LeaveRequestsController : ControllerBase
{
    private readonly ISender _sender;

    public LeaveRequestsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>提交请假申请（multipart/form-data，可携带多个附件，字段名 Attachments）。</summary>
    [HttpPost]
    public async Task<ActionResult<SubmitLeaveResultDto>> Submit(
        [FromForm] SubmitLeaveRequestCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetDetail), new { id = result.Id }, result);
    }

    /// <summary>我的请假申请列表（分页）。</summary>
    [HttpGet("my")]
    public async Task<ActionResult<PagedResult<LeaveRequestListItemDto>>> GetMy(
        [FromQuery] GetMyLeaveRequestsQuery query, CancellationToken cancellationToken)
    {
        return await _sender.Send(query, cancellationToken);
    }

    /// <summary>请假单详情（Redis 缓存优先）。仅申请人本人、该单据审批人、Admin 可见。</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<LeaveRequestDetailDto>> GetDetail(long id, CancellationToken cancellationToken)
    {
        return await _sender.Send(new GetLeaveRequestDetailQuery(id), cancellationToken);
    }

    /// <summary>审批同意。审批人身份来自 Token，不接受客户端传入。</summary>
    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<LeaveOperationResultDto>> Approve(
        long id, [FromBody] ApproveRequestBody body, CancellationToken cancellationToken)
    {
        return await _sender.Send(new ApproveLeaveRequestCommand(id, body?.Comment), cancellationToken);
    }

    /// <summary>审批驳回。驳回意见必填。</summary>
    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<LeaveOperationResultDto>> Reject(
        long id, [FromBody] RejectRequestBody body, CancellationToken cancellationToken)
    {
        return await _sender.Send(new RejectLeaveRequestCommand(id, body?.Comment ?? string.Empty), cancellationToken);
    }

    /// <summary>下载附件。需要对该单据的访问权限；不暴露物理路径。</summary>
    [HttpGet("{id:long}/attachments/{attachmentId:long}")]
    public async Task<IActionResult> DownloadAttachment(
        long id, long attachmentId, CancellationToken cancellationToken)
    {
        var file = await _sender.Send(new GetLeaveAttachmentQuery(id, attachmentId), cancellationToken);
        return File(file.Stream, file.ContentType, file.DownloadName);
    }

    public record ApproveRequestBody(string? Comment);

    public record RejectRequestBody(string? Comment);
}
