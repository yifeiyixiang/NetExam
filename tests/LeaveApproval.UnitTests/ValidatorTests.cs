using FluentAssertions;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Application.Features.Approvals.Queries;
using LeaveApproval.Application.Features.Auth;
using LeaveApproval.Application.Features.LeaveRequests.Commands;
using LeaveApproval.Application.Features.LeaveRequests.Queries;
using LeaveApproval.Application.Features.WorkflowConfigs.Commands;
using LeaveApproval.Application.Common.Models;
using LeaveApproval.Domain.Entities;
using LeaveApproval.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace LeaveApproval.UnitTests;

/// <summary>FluentValidation 验证器单元测试（直接构造验证器，不经过 MediatR）。</summary>
public class ValidatorTests
{
    private static SubmitLeaveRequestCommandValidator CreateSubmitValidator(
        int maxCount = 5, long maxSizeBytes = 10 * 1024 * 1024, decimal maxDurationHours = 720)
    {
        return new SubmitLeaveRequestCommandValidator(
            Options.Create(new AttachmentSettings
            {
                MaxCount = maxCount,
                MaxSizeBytes = maxSizeBytes,
                AllowedExtensions = new List<string> { ".jpg", ".png", ".pdf", ".txt" }
            }),
            Options.Create(new LeaveSettings { MaxDurationHours = maxDurationHours }));
    }

    private static IFormFile MakeFormFile(string fileName, long length)
    {
        var content = new byte[length > 0 ? Math.Min(length, 64) : 1];
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, length, "Attachments", fileName);
    }

    private static SubmitLeaveRequestCommand ValidCommand() => new()
    {
        LeaveType = LeaveType.Annual,
        StartTime = new DateTime(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc),
        EndTime = new DateTime(2026, 10, 27, 18, 0, 0, DateTimeKind.Utc),
        Reason = "家中有事"
    };

    [Fact]
    public async Task 请假_结束时间早于开始时间_校验失败()
    {
        var command = ValidCommand();
        command.EndTime = command.StartTime.AddHours(-1);

        var result = await CreateSubmitValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EndTime");
    }

    [Fact]
    public async Task 请假_结束时间等于开始时间_校验失败()
    {
        var command = ValidCommand();
        command.EndTime = command.StartTime;

        var result = await CreateSubmitValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 请假_原因为空_校验失败()
    {
        var command = ValidCommand();
        command.Reason = "";

        var result = await CreateSubmitValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Reason");
    }

    [Fact]
    public async Task 请假_原因超长_校验失败()
    {
        var command = ValidCommand();
        command.Reason = new string('长', 2001);

        var result = await CreateSubmitValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 请假_非法请假类型_校验失败()
    {
        var command = ValidCommand();
        command.LeaveType = (LeaveType)999;

        var result = await CreateSubmitValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LeaveType");
    }

    [Fact]
    public async Task 请假_超过最大时长_校验失败()
    {
        var command = ValidCommand();
        command.EndTime = command.StartTime.AddHours(800);

        var result = await CreateSubmitValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 请假_附件数量超上限_校验失败()
    {
        var command = ValidCommand();
        command.Attachments = Enumerable.Range(0, 6)
            .Select(i => MakeFormFile($"f{i}.txt", 10))
            .ToList();

        var result = await CreateSubmitValidator(maxCount: 5).ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Attachments"));
    }

    [Fact]
    public async Task 请假_附件超过大小上限_校验失败()
    {
        var command = ValidCommand();
        command.Attachments = new List<IFormFile> { MakeFormFile("big.txt", 11 * 1024 * 1024) };

        var result = await CreateSubmitValidator(maxSizeBytes: 10 * 1024 * 1024).ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 请假_附件类型不允许_校验失败()
    {
        var command = ValidCommand();
        command.Attachments = new List<IFormFile> { MakeFormFile("virus.exe", 10) };

        var result = await CreateSubmitValidator().ValidateAsync(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 请假_合法命令_校验通过()
    {
        var command = ValidCommand();
        command.Attachments = new List<IFormFile> { MakeFormFile("说明.txt", 100) };

        var result = await CreateSubmitValidator().ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task 登录_用户名或密码为空_校验失败()
    {
        var validator = new LoginCommandValidator();

        var result = await validator.ValidateAsync(new LoginCommand("", ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public async Task 驳回_意见为空_校验失败()
    {
        var validator = new RejectLeaveRequestCommandValidator();

        var result = await validator.ValidateAsync(new RejectLeaveRequestCommand(1, "  "));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 驳回_意见超长_校验失败()
    {
        var validator = new RejectLeaveRequestCommandValidator();

        var result = await validator.ValidateAsync(new RejectLeaveRequestCommand(1, new string('c', 501)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 流程配置_空节点列表_校验失败()
    {
        var validator = new CreateWorkflowConfigCommandValidator();

        var result = await validator.ValidateAsync(new CreateWorkflowConfigCommand
        {
            Name = "测试流程",
            Nodes = new List<WorkflowNodeInputDto>()
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 流程配置_节点顺序重复_校验失败()
    {
        var validator = new CreateWorkflowConfigCommandValidator();

        var result = await validator.ValidateAsync(new CreateWorkflowConfigCommand
        {
            Name = "测试流程",
            Nodes = new List<WorkflowNodeInputDto>
            {
                new() { NodeOrder = 1, NodeName = "节点一", ApproverType = ApproverType.DirectLeader },
                new() { NodeOrder = 1, NodeName = "节点二", ApproverType = ApproverType.DepartmentManager }
            }
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 流程配置_指定用户节点未配置审批人_校验失败()
    {
        var validator = new CreateWorkflowConfigCommandValidator();

        var result = await validator.ValidateAsync(new CreateWorkflowConfigCommand
        {
            Name = "测试流程",
            Nodes = new List<WorkflowNodeInputDto>
            {
                new() { NodeOrder = 1, NodeName = "经理审批", ApproverType = ApproverType.SpecificUser, ApproverUserId = null }
            }
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 待办查询_分页参数越界_校验失败()
    {
        var validator = new GetMyPendingApprovalsQueryValidator();

        var result = await validator.ValidateAsync(new GetMyPendingApprovalsQuery(Page: 0, PageSize: 500));

        result.IsValid.Should().BeFalse();
    }
}
