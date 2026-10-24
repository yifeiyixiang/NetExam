using Autofac;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Infrastructure.Email;
using LeaveApproval.Infrastructure.FileStorage;
using LeaveApproval.Infrastructure.Persistence;
using LeaveApproval.Infrastructure.Repositories;
using LeaveApproval.Infrastructure.Redis;
using LeaveApproval.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LeaveApproval.Infrastructure.DependencyInjection;

/// <summary>Infrastructure 模块：数据库、Redis、文件存储、邮件、JWT/密码、仓储与后台服务。</summary>
public class InfrastructureModule : Module
{
    private readonly IConfiguration _configuration;

    public InfrastructureModule(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override void Load(ContainerBuilder builder)
    {
        // ---- 数据库：DbContext / IUnitOfWork，作用域生命周期（严禁 Singleton）----
        var connectionString = _configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("缺少连接字符串配置 ConnectionStrings:Default");

        // MySQL 5.7：显式声明服务器版本，避免设计期/启动期 AutoDetect 连接
        var serverVersion = ServerVersion.Create(new Version(5, 7, 40), Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql);

        builder.Register(ctx =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseMySql(connectionString, serverVersion,
                    mySql => mySql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
                return optionsBuilder.Options;
            })
            .InstancePerLifetimeScope();

        builder.RegisterType<AppDbContext>()
            .AsSelf()
            .As<IUnitOfWork>()
            .InstancePerLifetimeScope();

        // ---- 仓储 ----
        builder.RegisterGeneric(typeof(BaseRepository<>))
            .As(typeof(IBaseRepository<>))
            .InstancePerLifetimeScope();

        // ---- Redis：ConnectionMultiplexer 全局单例复用连接（官方推荐）----
        var redisConnectionString = _configuration["Redis:ConnectionString"] ?? "localhost:6379";
        builder.Register(ctx => ConnectionMultiplexer.Connect(
                new ConfigurationOptions
                {
                    EndPoints = { redisConnectionString },
                    AbortOnConnectFail = false,
                    ConnectRetry = 3,
                    SyncTimeout = 3000,
                    AsyncTimeout = 3000
                }))
            .As<IConnectionMultiplexer>()
            .SingleInstance();

        builder.RegisterType<RedisCacheService>()
            .As<ICacheService>()
            .InstancePerLifetimeScope();

        // ---- 文件存储 ----
        var fileStorageSettings = _configuration.GetSection(FileStorageSettings.SectionName).Get<FileStorageSettings>()
            ?? new FileStorageSettings();
        builder.RegisterInstance(fileStorageSettings).AsSelf().SingleInstance();
        builder.RegisterType<LocalFileStorageService>()
            .As<IFileStorageService>()
            .SingleInstance();

        // ---- 邮件（模拟实现 + Outbox 后台处理器）----
        builder.RegisterType<InMemoryEmailStore>()
            .As<IInMemoryEmailStore>()
            .SingleInstance();
        builder.RegisterType<SimulatedEmailService>()
            .As<IEmailService>()
            .InstancePerLifetimeScope();
        builder.RegisterType<EmailOutboxProcessor>()
            .As<IHostedService>()
            .SingleInstance();

        // ---- 认证 ----
        var jwtSettings = _configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? new JwtSettings();
        builder.RegisterInstance(jwtSettings).AsSelf().SingleInstance();
        builder.RegisterType<JwtTokenService>().As<ITokenService>().InstancePerLifetimeScope();
        builder.RegisterType<BCryptPasswordHasher>().As<IPasswordHasher>().SingleInstance();
    }
}
