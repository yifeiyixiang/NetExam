using LeaveApproval.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveApproval.Api.Controllers;

/// <summary>开发辅助接口（仅 Admin）：查看模拟邮件（内存邮箱）。</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly IInMemoryEmailStore _emailStore;

    public DevController(IInMemoryEmailStore emailStore)
    {
        _emailStore = emailStore;
    }

    /// <summary>最近发送的模拟邮件（Outbox 处理器发送后写入）。</summary>
    [HttpGet("emails")]
    public ActionResult<object> GetEmails([FromQuery] int limit = 50)
    {
        var emails = _emailStore.Snapshot().Take(Math.Clamp(limit, 1, 200));
        return Ok(new
        {
            items = emails.Select(e => new
            {
                to = e.Message.To,
                subject = e.Message.Subject,
                body = e.Message.Body,
                receivedAt = e.ReceivedAtUtc
            })
        });
    }
}
