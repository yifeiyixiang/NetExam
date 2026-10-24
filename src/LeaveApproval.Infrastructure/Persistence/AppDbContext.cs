using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Domain.Common;
using LeaveApproval.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LeaveApproval.Infrastructure.Persistence;

/// <summary>工作单元：DbContext 负责变更跟踪、并发令牌自增与数据库事务。</summary>
public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveAttachment> LeaveAttachments => Set<LeaveAttachment>();
    public DbSet<WorkflowConfig> WorkflowConfigs => Set<WorkflowConfig>();
    public DbSet<WorkflowNode> WorkflowNodes => Set<WorkflowNode>();
    public DbSet<LeaveApprovalTask> LeaveApprovalTasks => Set<LeaveApprovalTask>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();

    private IDbContextTransaction? _currentTransaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        try
        {
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 乐观并发令牌自增：配合 IsConcurrencyToken 生成条件更新（WHERE Version = 旧值）
        foreach (var entry in ChangeTracker.Entries<IHasConcurrencyVersion>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version++;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
