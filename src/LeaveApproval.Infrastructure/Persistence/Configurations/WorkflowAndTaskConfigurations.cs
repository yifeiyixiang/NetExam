using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LeaveApproval.Infrastructure.Persistence.Configurations;

public class WorkflowConfigConfiguration : IEntityTypeConfiguration<WorkflowConfig>
{
    public void Configure(EntityTypeBuilder<WorkflowConfig> builder)
    {
        builder.ToTable("WorkflowConfigs");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(c => new { c.DepartmentId, c.IsActive }).HasDatabaseName("IX_WorkflowConfigs_Department_IsActive");

        builder.HasOne(c => c.Department)
            .WithMany()
            .HasForeignKey(c => c.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.UpdatedBy)
            .WithMany()
            .HasForeignKey(c => c.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class WorkflowNodeConfiguration : IEntityTypeConfiguration<WorkflowNode>
{
    public void Configure(EntityTypeBuilder<WorkflowNode> builder)
    {
        builder.ToTable("WorkflowNodes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.NodeName).HasMaxLength(50).IsRequired();
        builder.Property(n => n.ApproverType).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(n => new { n.WorkflowConfigId, n.NodeOrder })
            .HasDatabaseName("IX_WorkflowNodes_ConfigId_Order");

        builder.HasOne(n => n.WorkflowConfig)
            .WithMany(c => c.Nodes)
            .HasForeignKey(n => n.WorkflowConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.ApproverUser)
            .WithMany()
            .HasForeignKey(n => n.ApproverUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class LeaveApprovalTaskConfiguration : IEntityTypeConfiguration<LeaveApprovalTask>
{
    public void Configure(EntityTypeBuilder<LeaveApprovalTask> builder)
    {
        builder.ToTable("LeaveApprovalTasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.NodeName).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // 并发控制：同一任务并发审批时，第二个事务因 Status/Version 不匹配更新 0 行而失败
        builder.Property(t => t.Version).IsConcurrencyToken();

        builder.HasIndex(t => new { t.ApproverUserId, t.Status, t.CreatedAt })
            .HasDatabaseName("IX_LeaveApprovalTasks_Approver_Status_CreatedAt");
        builder.HasIndex(t => new { t.LeaveRequestId, t.NodeOrder })
            .HasDatabaseName("IX_LeaveApprovalTasks_LeaveId_Order");
        builder.HasIndex(t => new { t.LeaveRequestId, t.WorkflowNodeId, t.ApproverUserId })
            .IsUnique().HasDatabaseName("UX_LeaveApprovalTasks_Leave_Node_Approver");

        builder.HasOne(t => t.LeaveRequest)
            .WithMany(l => l.Tasks)
            .HasForeignKey(t => t.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.WorkflowNode)
            .WithMany()
            .HasForeignKey(t => t.WorkflowNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ApproverUser)
            .WithMany()
            .HasForeignKey(t => t.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApprovalRecordConfiguration : IEntityTypeConfiguration<ApprovalRecord>
{
    public void Configure(EntityTypeBuilder<ApprovalRecord> builder)
    {
        builder.ToTable("ApprovalRecords");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.OperatorName).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(500);
        builder.Property(r => r.FromStatus).HasMaxLength(20);
        builder.Property(r => r.ToStatus).HasMaxLength(20);

        builder.HasIndex(r => new { r.LeaveRequestId, r.CreatedAt }).HasDatabaseName("IX_ApprovalRecords_LeaveId_CreatedAt");

        builder.HasOne(r => r.LeaveRequest)
            .WithMany(l => l.Records)
            .HasForeignKey(r => r.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkflowNode>()
            .WithMany()
            .HasForeignKey(r => r.WorkflowNodeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageType).HasMaxLength(50).IsRequired();
        builder.Property(m => m.BusinessKey).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Recipient).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Subject).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Body).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(1000);

        builder.HasIndex(m => m.BusinessKey).HasDatabaseName("IX_EmailOutbox_BusinessKey");
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt }).HasDatabaseName("IX_EmailOutbox_Status_NextAttempt");
    }
}
