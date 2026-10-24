using Autofac;
using Autofac.Extensions.DependencyInjection;
using LeaveApproval.Api.Extensions;
using LeaveApproval.Api.Filters;
using LeaveApproval.Api.Middleware;
using LeaveApproval.Api.Modules;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.DependencyInjection;
using LeaveApproval.Infrastructure.DependencyInjection;
using LeaveApproval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

// 关联 ID、请求名称等由中间件/管道通过 LogContext 注入（结构化属性，不拼接字符串）
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host
        .UseSerilog((context, _, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext())
        .UseServiceProviderFactory(new AutofacServiceProviderFactory());

    builder.Host.ConfigureContainer<ContainerBuilder>(container =>
    {
        container.RegisterModule(new InfrastructureModule(builder.Configuration));
        container.RegisterModule(new ApplicationModule());
        container.RegisterModule(new ApiModule());
    });

    builder.Services
        .AddCurrentUserFilter()
        .AddJwtAuthentication(builder.Configuration)
        .AddAppSettings(builder.Configuration)
        .AddSwaggerWithJwt()
        .AddHealthChecks();

    // 前端页面与 API 同源部署；允许跨域便于页面单独托管（使用 Bearer Token，不涉及 Cookie）
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} => {StatusCode}，耗时 {Elapsed:0.0} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
            diagnosticContext.Set("UserName", httpContext.User.FindFirst("name")?.Value);
        };
        options.GetLevel = (httpContext, elapsed, ex) => ex != null
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error : LogEventLevel.Information;
    });
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 托管 wwwroot/index.html 前端页面（访问 http://localhost:5000/ 即可测试）
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseCors();

    // Swagger：默认全环境开放以便验收，生产环境可通过 Swagger:Enabled=false 关闭
    if (app.Configuration.GetValue<bool>("Swagger:Enabled", true))
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "LeaveApproval API v1");
            options.DocumentTitle = "请假审批系统 API";
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    // 开发/测试环境：启动时执行 Migration 并写入种子数据（可通过 Database:InitializeOnStartup 控制）
    var initializeDatabase = app.Configuration.GetValue<bool>("Database:InitializeOnStartup")
                             || app.Environment.IsDevelopment();
    if (initializeDatabase)
    {
        await InitializeDatabaseAsync(app);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "应用启动失败");
}
finally
{
    Log.CloseAndFlush();
}

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

    logger.LogInformation("正在执行数据库迁移…");
    await db.Database.MigrateAsync();
    logger.LogInformation("数据库迁移完成");

    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DbSeeder.SeedAsync(db, passwordHasher, logger);
}

// 供集成测试 WebApplicationFactory<Program> 使用
public partial class Program
{
}
