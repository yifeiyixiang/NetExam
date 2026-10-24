using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LeaveApproval.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).HasMaxLength(50).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(20).IsRequired();

        builder.HasIndex(u => u.Username).IsUnique().HasDatabaseName("UX_Users_Username");
        builder.HasIndex(u => u.DepartmentId).HasDatabaseName("IX_Users_DepartmentId");

        builder.HasOne(u => u.Department)
            .WithMany()
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(u => u.DirectManager)
            .WithMany()
            .HasForeignKey(u => u.DirectManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(d => d.Name).IsUnique().HasDatabaseName("UX_Departments_Name");

        builder.HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LeaveNo).HasMaxLength(32).IsRequired();
        builder.Property(l => l.LeaveType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(l => l.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(l => l.DurationHours).HasPrecision(6, 1);

        builder.HasIndex(l => l.LeaveNo).IsUnique().HasDatabaseName("UX_LeaveRequests_LeaveNo");
        builder.HasIndex(l => new { l.ApplicantId, l.CreatedAt }).HasDatabaseName("IX_LeaveRequests_ApplicantId_CreatedAt");
        builder.HasIndex(l => new { l.Status, l.CurrentNodeOrder }).HasDatabaseName("IX_LeaveRequests_Status_CurrentNode");

        builder.HasOne(l => l.Applicant)
            .WithMany()
            .HasForeignKey(l => l.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Department)
            .WithMany()
            .HasForeignKey(l => l.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkflowConfig>()
            .WithMany()
            .HasForeignKey(l => l.WorkflowConfigId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.Version).IsConcurrencyToken();
    }
}

public class LeaveAttachmentConfiguration : IEntityTypeConfiguration<LeaveAttachment>
{
    public void Configure(EntityTypeBuilder<LeaveAttachment> builder)
    {
        builder.ToTable("LeaveAttachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(64).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();

        builder.HasIndex(a => new { a.LeaveRequestId, a.CreatedAt }).HasDatabaseName("IX_LeaveAttachments_LeaveId_CreatedAt");

        builder.HasOne(a => a.LeaveRequest)
            .WithMany(l => l.Attachments)
            .HasForeignKey(a => a.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UploadedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
