using AutoMapper;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;

namespace LeaveApproval.Application.Mappings;

/// <summary>请假单相关实体 → DTO 映射（独立 Profile，不在 Handler 中手写赋值）。</summary>
public class LeaveRequestMappingProfile : Profile
{
    public LeaveRequestMappingProfile()
    {
        CreateMap<User, UserSummaryDto>()
            .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : null));

        CreateMap<LeaveApprovalTask, ApprovalTaskDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.ApproverName, o => o.MapFrom(s => s.ApproverUser != null ? s.ApproverUser.DisplayName : null));

        CreateMap<ApprovalRecord, ApprovalRecordDto>()
            .ForMember(d => d.Action, o => o.MapFrom(s => s.Action.ToString()));

        CreateMap<LeaveAttachment, LeaveAttachmentDto>()
            .ForMember(d => d.DownloadUrl,
                o => o.MapFrom(s => $"/api/leave-requests/{s.LeaveRequestId}/attachments/{s.Id}"));

        CreateMap<LeaveRequest, LeaveRequestDetailDto>()
            .ForMember(d => d.LeaveType, o => o.MapFrom(s => s.LeaveType.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Applicant, o => o.MapFrom(s => s.Applicant))
            .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name : null))
            .ForMember(d => d.CurrentNodeName, o => o.MapFrom(
                s => s.Tasks.Where(t => t.NodeOrder == s.CurrentNodeOrder)
                         .Select(t => t.NodeName)
                         .FirstOrDefault()))
            .ForMember(d => d.Tasks, o => o.MapFrom(s => s.Tasks.OrderBy(t => t.NodeOrder).ThenBy(t => t.Id)))
            .ForMember(d => d.Records, o => o.MapFrom(s => s.Records.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)))
            .ForMember(d => d.Attachments, o => o.MapFrom(s => s.Attachments.OrderBy(a => a.Id).ToList()));

        CreateMap<LeaveRequest, LeaveRequestListItemDto>()
            .ForMember(d => d.LeaveType, o => o.MapFrom(s => s.LeaveType.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.CurrentNodeName, o => o.MapFrom(
                s => s.Tasks.Where(t => t.NodeOrder == s.CurrentNodeOrder)
                         .Select(t => t.NodeName)
                         .FirstOrDefault()));

        // 我的待办列表：LeaveApprovalTask -> PendingApprovalItemDto
        CreateMap<LeaveApprovalTask, PendingApprovalItemDto>()
            .ForMember(d => d.LeaveRequestId, o => o.MapFrom(s => s.LeaveRequestId))
            .ForMember(d => d.LeaveNo, o => o.MapFrom(s => s.LeaveRequest != null ? s.LeaveRequest.LeaveNo : null!))
            .ForMember(d => d.ApplicantName, o => o.MapFrom(
                s => s.LeaveRequest != null && s.LeaveRequest.Applicant != null
                    ? s.LeaveRequest.Applicant.DisplayName
                    : null!))
            .ForMember(d => d.DepartmentName, o => o.MapFrom(
                s => s.LeaveRequest != null && s.LeaveRequest.Department != null
                    ? s.LeaveRequest.Department.Name
                    : null))
            .ForMember(d => d.LeaveType, o => o.MapFrom(
                s => s.LeaveRequest != null ? s.LeaveRequest.LeaveType.ToString() : null!))
            .ForMember(d => d.StartTime, o => o.MapFrom(s => s.LeaveRequest != null ? s.LeaveRequest.StartTime : default))
            .ForMember(d => d.EndTime, o => o.MapFrom(s => s.LeaveRequest != null ? s.LeaveRequest.EndTime : default))
            .ForMember(d => d.DurationHours, o => o.MapFrom(s => s.LeaveRequest != null ? s.LeaveRequest.DurationHours : 0m))
            .ForMember(d => d.Reason, o => o.MapFrom(s => s.LeaveRequest != null ? s.LeaveRequest.Reason : null!))
            .ForMember(d => d.SubmittedAt, o => o.MapFrom(s => s.LeaveRequest != null ? s.LeaveRequest.SubmittedAt ?? s.LeaveRequest.CreatedAt : s.CreatedAt))
            .ForMember(d => d.TaskId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.NodeOrder, o => o.MapFrom(s => s.NodeOrder))
            .ForMember(d => d.NodeName, o => o.MapFrom(s => s.NodeName));
    }
}
