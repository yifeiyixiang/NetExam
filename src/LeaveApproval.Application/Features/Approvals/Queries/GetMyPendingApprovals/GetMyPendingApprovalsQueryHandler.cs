using AutoMapper;
using LeaveApproval.Application.Common;
using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.Common.Models;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Application.Features.Approvals.Queries;

public class GetMyPendingApprovalsQueryHandler : IRequestHandler<GetMyPendingApprovalsQuery, PagedResult<PendingApprovalItemDto>>
{
    private static readonly TimeSpan PendingCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IBaseRepository<LeaveApprovalTask> _tasks;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetMyPendingApprovalsQueryHandler> _logger;

    public GetMyPendingApprovalsQueryHandler(
        IBaseRepository<LeaveApprovalTask> tasks,
        ICacheService cache,
        IMapper mapper,
        ICurrentUserService currentUser,
        ILogger<GetMyPendingApprovalsQueryHandler> logger)
    {
        _tasks = tasks;
        _cache = cache;
        _mapper = mapper;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<PagedResult<PendingApprovalItemDto>> Handle(GetMyPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new AuthenticationFailedException("未获取到当前用户身份");

        // 读取该用户的待办缓存版本号（不存在视为 1），缓存键包含用户、版本、页码、每页大小
        var version = await _cache.GetLongAsync(CacheKeys.PendingVersion(userId), cancellationToken) ?? 1L;
        var cacheKey = CacheKeys.PendingList(userId, version, request.Page, request.PageSize);

        var cached = await _cache.GetAsync<PagedResult<PendingApprovalItemDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("命中待办缓存：UserId={UserId} Page={Page} PageSize={PageSize}", userId, request.Page, request.PageSize);
            return cached;
        }

        var query = _tasks.Query()
            .AsNoTracking()
            .Include(t => t.LeaveRequest).ThenInclude(l => l!.Applicant)
            .Include(t => t.LeaveRequest).ThenInclude(l => l!.Department)
            .Where(t => t.ApproverUserId == userId &&
                        t.Status == ApprovalTaskStatus.Pending &&
                        t.LeaveRequest != null &&
                        t.LeaveRequest.Status == LeaveStatus.PendingApproval)
            .OrderByDescending(t => t.LeaveRequest!.SubmittedAt)
            .ThenBy(t => t.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var tasks = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<PendingApprovalItemDto>
        {
            Items = _mapper.Map<List<PendingApprovalItemDto>>(tasks),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        await _cache.SetAsync(cacheKey, result, PendingCacheTtl, cancellationToken);
        return result;
    }
}
