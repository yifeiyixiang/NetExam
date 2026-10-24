using LeaveApproval.Application.DTOs;
using LeaveApproval.Application.Features.WorkflowConfigs.Commands;
using LeaveApproval.Application.Features.WorkflowConfigs.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveApproval.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/workflow-configs")]
public class WorkflowConfigsController : ControllerBase
{
    private readonly ISender _sender;

    public WorkflowConfigsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>创建流程配置（仅 Admin）。</summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowConfigDto>> Create(
        CreateWorkflowConfigCommand command, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>修改流程配置（仅 Admin）。已被单据使用的流程节点结构锁定。</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<WorkflowConfigDto>> Update(
        long id, UpdateWorkflowConfigCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        return await _sender.Send(command, cancellationToken);
    }

    /// <summary>流程配置详情（仅 Admin）。</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<WorkflowConfigDto>> GetById(long id, CancellationToken cancellationToken)
    {
        return await _sender.Send(new GetWorkflowConfigQuery(id), cancellationToken);
    }

    /// <summary>流程配置列表（仅 Admin）。</summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkflowConfigDto>>> GetList(CancellationToken cancellationToken)
    {
        return await _sender.Send(new GetWorkflowConfigListQuery(), cancellationToken);
    }
}
