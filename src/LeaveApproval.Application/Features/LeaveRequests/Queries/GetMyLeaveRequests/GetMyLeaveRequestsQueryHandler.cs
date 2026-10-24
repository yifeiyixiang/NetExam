using AutoMapper;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.Common.Models;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LeaveApproval.Application.Features.LeaveRequests.Queries;

public class GetMyLeaveRequestsQueryHandler : IRequestHandler<GetMyLeaveRequestsQuery, PagedResult<LeaveRequestListItemDto>>
{
    private readonly IBaseRepository<LeaveRequest> _leaveRequests;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;

    public GetMyLeaveRequestsQueryHandler(
        IBaseRepository<LeaveRequest> leaveRequests,
        IMapper mapper,
        ICurrentUserService currentUser)
    {
        _leaveRequests = leaveRequests;
        _mapper = mapper;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<LeaveRequestListItemDto>> Handle(GetMyLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("未获取到当前用户身份");

        var query = _leaveRequests.Query()
            .AsNoTracking()
            .Include(l => l.Tasks)
            .Where(l => l.ApplicantId == userId)
            .OrderByDescending(l => l.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LeaveRequestListItemDto>
        {
            Items = _mapper.Map<List<LeaveRequestListItemDto>>(items),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }
}
