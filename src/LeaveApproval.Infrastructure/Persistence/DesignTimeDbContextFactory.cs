using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LeaveApproval.Infrastructure.Persistence;

/// <summary>设计时 DbContext 工厂（dotnet ef migrations 使用，不连接数据库）。</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Port=10010;Database=local;User=root;Password=root;charset=utf8mb4";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(connectionString, ServerVersion.Create(new Version(5, 7, 40), Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql),
            mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        return new AppDbContext(optionsBuilder.Options);
    }
}
