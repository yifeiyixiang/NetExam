using AutoMapper;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;

namespace LeaveApproval.Application.Mappings;

public class WorkflowMappingProfile : Profile
{
    public WorkflowMappingProfile()
    {
        CreateMap<WorkflowNode, WorkflowNodeDto>()
            .ForMember(d => d.ApproverType, o => o.MapFrom(s => s.ApproverType.ToString()))
            .ForMember(d => d.ApproverUserName,
                o => o.MapFrom(s => s.ApproverUser != null ? s.ApproverUser.DisplayName : null));

        CreateMap<WorkflowConfig, WorkflowConfigDto>()
            .ForMember(d => d.DepartmentName,
                o => o.MapFrom(s => s.Department != null ? s.Department.Name : null))
            .ForMember(d => d.Nodes, o => o.MapFrom(s => s.Nodes.OrderBy(n => n.NodeOrder).ToList()));
    }
}
