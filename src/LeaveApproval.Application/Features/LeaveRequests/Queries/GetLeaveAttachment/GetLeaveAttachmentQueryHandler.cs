using LeaveApproval.Application.Common;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LeaveApproval.Application.Features.LeaveRequests.Queries;

public class GetLeaveAttachmentQueryHandler : IRequestHandler<GetLeaveAttachmentQuery, LeaveAttachmentFile>
{
    private readonly IBaseRepository<LeaveRequest> _leaveRequests;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;

    public GetLeaveAttachmentQueryHandler(
        IBaseRepository<LeaveRequest> leaveRequests,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser)
    {
        _leaveRequests = leaveRequests;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
    }

    public async Task<LeaveAttachmentFile> Handle(GetLeaveAttachmentQuery request, CancellationToken cancellationToken)
    {
        var leave = await _leaveRequests.Query()
            .AsNoTracking()
            .Include(l => l.Tasks)
            .Include(l => l.Attachments)
            .FirstOrDefaultAsync(l => l.Id == request.LeaveRequestId, cancellationToken)
            ?? throw new NotFoundException("请假单", request.LeaveRequestId);

        // 对象级授权：申请人本人、该单据的审批任务审批人、Admin
        LeaveAccessPolicy.EnsureCanView(leave.ApplicantId, leave.Tasks.Select(t => t.ApproverUserId), _currentUser);

        var attachment = leave.Attachments.FirstOrDefault(a => a.Id == request.AttachmentId)
            ?? throw new NotFoundException("附件", request.AttachmentId);

        // 存储键由本地存储实现校验（防路径穿越），物理路径不暴露
        var stream = await _fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);

        return new LeaveAttachmentFile(stream, attachment.OriginalFileName, attachment.ContentType);
    }
}
