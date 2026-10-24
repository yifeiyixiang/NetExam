using Autofac;
using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LeaveApproval.Application.DependencyInjection;

/// <summary>Application 模块：MediatR、管道行为、验证器、AutoMapper、PostCommit 执行器。</summary>
public class ApplicationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var applicationAssembly = typeof(ApplicationModule).Assembly;

        // MediatR：IMediator / ISender / IPublisher（每个请求作用域独立）。
        // 当前环境的 MediatR 构建版本 Mediator 构造函数注入 IServiceProvider，
        // Autofac 会在每个作用域提供 IServiceProvider，Handler/Behavior 从同一作用域解析。
        builder.RegisterType<Mediator>()
            .As<IMediator>().As<ISender>().As<IPublisher>()
            .InstancePerLifetimeScope();

        // 请求处理器（CQRS Handler）
        builder.RegisterAssemblyTypes(applicationAssembly)
            .AsClosedTypesOf(typeof(IRequestHandler<,>))
            .InstancePerDependency();

        // 管道行为：注册顺序即执行顺序 —— 日志 → 校验 → 事务
        builder.RegisterGeneric(typeof(Common.Behaviors.LoggingBehavior<,>))
            .As(typeof(IPipelineBehavior<,>)).InstancePerDependency();
        builder.RegisterGeneric(typeof(Common.Behaviors.ValidationBehavior<,>))
            .As(typeof(IPipelineBehavior<,>)).InstancePerDependency();
        builder.RegisterGeneric(typeof(Common.Behaviors.TransactionBehavior<,>))
            .As(typeof(IPipelineBehavior<,>)).InstancePerDependency();

        // FluentValidation 验证器
        builder.RegisterAssemblyTypes(applicationAssembly)
            .Where(t => t.IsClosedTypeOf(typeof(IValidator<>)))
            .AsImplementedInterfaces()
            .InstancePerDependency();

        // AutoMapper：独立 Profile
        builder.Register(ctx => new MapperConfiguration(
                cfg => cfg.AddMaps(applicationAssembly),
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance))
            .AsSelf()
            .SingleInstance();
        builder.Register(ctx => ctx.Resolve<MapperConfiguration>().CreateMapper())
            .As<IMapper>()
            .InstancePerLifetimeScope();

        // 事务提交后回调执行器
        builder.RegisterType<Common.PostCommitExecutor>()
            .As<Common.Interfaces.IPostCommitExecutor>()
            .InstancePerLifetimeScope();
    }
}
