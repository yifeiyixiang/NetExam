using System.Linq.Expressions;

namespace LeaveApproval.Application.Common.Interfaces;

/// <summary>基础仓储抽象。查询入口返回 IQueryable，但禁止将 IQueryable 直接返回给 API 客户端。</summary>
public interface IBaseRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Delete(TEntity entity);

    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>受控查询入口，配合 AsNoTracking / Include 使用。</summary>
    IQueryable<TEntity> Query();
}
