using Autofac;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.DependencyInjection;

namespace LeaveApproval.Api.Modules;

/// <summary>API 模块：当前用户上下文等 HTTP 相关服务。</summary>
public class ApiModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<Services.CurrentUserService>()
            .As<ICurrentUserService>()
            .InstancePerLifetimeScope();
    }
}
