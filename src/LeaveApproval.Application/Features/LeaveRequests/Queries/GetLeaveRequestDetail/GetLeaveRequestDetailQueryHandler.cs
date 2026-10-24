using AutoMapper;
using LeaveApproval.Application.Common;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Application.Features.LeaveRequests.Queries;

public class GetLeaveRequestDetailQueryHandler : IRequestHandler<GetLeaveRequestDetailQuery, LeaveRequestDetailDto>
{
    private static readonly TimeSpan DetailCacheTtl = TimeSpan.FromMinutes(10);

    private readonly IBaseRepository<LeaveRequest> _leaveRequests;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetLeaveRequestDetailQueryHandler> _logger;

    public GetLeaveRequestDetailQueryHandler(
        IBaseRepository<LeaveRequest> leaveRequests,
        ICacheService cache,
        IMapper mapper,
        ICurrentUserService currentUser,
        ILogger<GetLeaveRequestDetailQueryHandler> logger)
    {
        _leaveRequests = leaveRequests;
        _cache = cache;
        _mapper = mapper;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<LeaveRequestDetailDto> Handle(GetLeaveRequestDetailQuery request, CancellationToken cancellationToken)
    {
        // 1. 优先读缓存（缓存未命中时才走数据库）
        var cached = await _cache.GetAsync<LeaveRequestDetailDto>(CacheKeys.Detail(request.Id), cancellationToken);
        if (cached != null)
        {
            EnsureCanView(cached);
            return cached;
        }

        // 2. 缓存未命中：EF Core 查询 + AutoMapper 映射
        var leave = await _leaveRequests.Query()
            .AsNoTracking()
            .Include(l => l.Applicant).ThenInclude(u => u!.Department)
            .Include(l => l.Department)
            .Include(l => l.Tasks).ThenInclude(t => t.ApproverUser)
            .Include(l => l.Records)
            .Include(l => l.Attachments)
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("请假单", request.Id);

        var dto = _mapper.Map<LeaveRequestDetailDto>(leave);

        // 3. 回写缓存
        await _cache.SetAsync(CacheKeys.Detail(request.Id), dto, DetailCacheTtl, cancellationToken);

        EnsureCanView(dto);
        return dto;
    }

    private void EnsureCanView(LeaveRequestDetailDto dto)
    {
        var approverIds = dto.Tasks.Select(t => t.ApproverUserId);
        LeaveAccessPolicy.EnsureCanView(dto.Applicant?.Id ?? -1, approverIds, _currentUser);
        _logger.LogDebug("请假单详情访问校验通过：LeaveRequestId={LeaveRequestId}", dto.Id);
    }
}
