using System.Linq.Expressions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeaveApproval.Infrastructure.Repositories;

/// <summary>基础仓储实现。复杂查询可另建专用查询仓储，不强迫使用通用仓储。</summary>
public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class
{
    protected readonly AppDbContext Context;

    public BaseRepository(AppDbContext context)
    {
        Context = context;
    }

    public virtual async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await Context.Set<TEntity>().FindAsync(new[] { id }, cancellationToken);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Context.Set<TEntity>().AddAsync(entity, cancellationToken);
    }

    public virtual void Update(TEntity entity)
    {
        Context.Set<TEntity>().Update(entity);
    }

    public virtual void Delete(TEntity entity)
    {
        Context.Set<TEntity>().Remove(entity);
    }

    public virtual Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Context.Set<TEntity>().AnyAsync(predicate, cancellationToken);
    }

    /// <summary>受控查询入口。查询结果必须先物化为 DTO，禁止把 IQueryable 直接返回给 API 客户端。</summary>
    public virtual IQueryable<TEntity> Query()
    {
        return Context.Set<TEntity>();
    }
}
