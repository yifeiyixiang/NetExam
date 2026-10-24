using AutoMapper;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LeaveApproval.Application.Features.WorkflowConfigs.Queries;

public class GetWorkflowConfigQueryHandler : IRequestHandler<GetWorkflowConfigQuery, WorkflowConfigDto>
{
    private readonly IBaseRepository<WorkflowConfig> _configs;
    private readonly IMapper _mapper;

    public GetWorkflowConfigQueryHandler(IBaseRepository<WorkflowConfig> configs, IMapper mapper)
    {
        _configs = configs;
        _mapper = mapper;
    }

    public async Task<WorkflowConfigDto> Handle(GetWorkflowConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _configs.Query()
            .AsNoTracking()
            .Include(c => c.Department)
            .Include(c => c.Nodes).ThenInclude(n => n.ApproverUser)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("流程配置", request.Id);

        return _mapper.Map<WorkflowConfigDto>(config);
    }
}

public class GetWorkflowConfigListQueryHandler : IRequestHandler<GetWorkflowConfigListQuery, List<WorkflowConfigDto>>
{
    private readonly IBaseRepository<WorkflowConfig> _configs;
    private readonly IMapper _mapper;

    public GetWorkflowConfigListQueryHandler(IBaseRepository<WorkflowConfig> configs, IMapper mapper)
    {
        _configs = configs;
        _mapper = mapper;
    }

    public async Task<List<WorkflowConfigDto>> Handle(GetWorkflowConfigListQuery request, CancellationToken cancellationToken)
    {
        var configs = await _configs.Query()
            .AsNoTracking()
            .Include(c => c.Department)
            .Include(c => c.Nodes).ThenInclude(n => n.ApproverUser)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<WorkflowConfigDto>>(configs);
    }
}
