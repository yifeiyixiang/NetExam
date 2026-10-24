using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Domain.Common;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Infrastructure.Persistence;

/// <summary>
/// 开发/测试环境种子数据（幂等）。生产环境禁止使用默认密码。
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher passwordHasher, ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            logger.LogInformation("数据库已有用户，跳过种子数据");
            return;
        }

        await db.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;

            var yanfa = new Department { Name = "研发部", CreatedAt = now };
            var shichang = new Department { Name = "市场部", CreatedAt = now };
            db.Departments.AddRange(yanfa, shichang);
            await db.SaveChangesAsync();

            var admin = NewUser("admin", "系统管理员", "admin@example.com", yanfa, null, Roles.Admin, "Admin@123", passwordHasher, now);
            var wangwu = NewUser("wangwu", "王五（研发部经理）", "wangwu@example.com", yanfa, null, Roles.Manager, "Manager@123", passwordHasher, now);
            var lisi = NewUser("lisi", "李四（直属领导）", "lisi@example.com", yanfa, null, Roles.Manager, "Manager@123", passwordHasher, now);
            var zhangsan = NewUser("zhangsan", "张三", "zhangsan@example.com", yanfa, null, Roles.Employee, "Employee@123", passwordHasher, now);
            var sunqi = NewUser("sunqi", "孙七（市场部经理）", "sunqi@example.com", shichang, null, Roles.Manager, "Manager@123", passwordHasher, now);
            var zhaoliu = NewUser("zhaoliu", "赵六", "zhaoliu@example.com", shichang, null, Roles.Employee, "Employee@123", passwordHasher, now);

            db.Users.AddRange(admin, wangwu, lisi, zhangsan, sunqi, zhaoliu);
            await db.SaveChangesAsync();

            // 主键生成后再建立自引用关系（直属领导链）与部门经理
            lisi.DirectManagerId = wangwu.Id;
            zhangsan.DirectManagerId = lisi.Id;
            zhaoliu.DirectManagerId = sunqi.Id;
            yanfa.ManagerUserId = wangwu.Id;
            shichang.ManagerUserId = sunqi.Id;
            await db.SaveChangesAsync();

            var config = new WorkflowConfig
            {
                Name = "研发部请假审批流程",
                DepartmentId = yanfa.Id,
                Version = 1,
                IsActive = true,
                CreatedByUserId = admin.Id,
                CreatedAt = now,
                Nodes =
                {
                    new WorkflowNode { NodeOrder = 1, NodeName = "直属领导审批", ApproverType = ApproverType.DirectLeader, IsRequired = true },
                    new WorkflowNode { NodeOrder = 2, NodeName = "部门经理审批", ApproverType = ApproverType.DepartmentManager, IsRequired = true }
                }
            };
            db.WorkflowConfigs.Add(config);
            await db.SaveChangesAsync();

            await db.CommitTransactionAsync();
            logger.LogInformation("种子数据写入完成：6 个用户、2 个部门、1 个流程配置");
        }
        catch
        {
            await db.RollbackTransactionAsync();
            throw;
        }
    }

    private static User NewUser(
        string username, string displayName, string email,
        Department department, long? directManagerId, string role,
        string password, IPasswordHasher passwordHasher, DateTime now)
    {
        return new User
        {
            Username = username,
            DisplayName = displayName,
            Email = email,
            DepartmentId = department.Id,
            DirectManagerId = directManagerId,
            Role = role,
            IsActive = true,
            PasswordHash = passwordHasher.Hash(password),
            CreatedAt = now
        };
    }
}
