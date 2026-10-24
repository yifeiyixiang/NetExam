using FluentValidation;
using LeaveApproval.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace LeaveApproval.Application.Features.LeaveRequests.Commands;

public class SubmitLeaveRequestCommandValidator : AbstractValidator<SubmitLeaveRequestCommand>
{
    public SubmitLeaveRequestCommandValidator(
        IOptions<AttachmentSettings> attachmentOptions,
        IOptions<LeaveSettings> leaveOptions)
    {
        var attachmentSettings = attachmentOptions.Value;
        var leaveSettings = leaveOptions.Value;

        RuleFor(x => x.LeaveType)
            .IsInEnum().WithMessage("请假类型必须是允许的枚举值");

        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("请假开始时间不能为空");

        RuleFor(x => x.EndTime)
            .NotEmpty().WithMessage("请假结束时间不能为空")
            .GreaterThan(x => x.StartTime).WithMessage("请假开始时间必须早于结束时间");

        RuleFor(x => x)
            .Must(x => x.StartTime != default &&
                       (decimal)(x.EndTime - x.StartTime).TotalHours <= leaveSettings.MaxDurationHours)
            .WithMessage($"请假时长不能超过 {leaveSettings.MaxDurationHours} 小时")
            .When(x => x.StartTime != default && x.EndTime != default)
            .WithName("EndTime");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("请假原因不能为空")
            .MaximumLength(2000).WithMessage("请假原因不能超过 2000 字");

        RuleFor(x => x.Attachments)
            .Must(a => a == null || a.Count <= attachmentSettings.MaxCount)
            .WithMessage($"附件数量不能超过 {attachmentSettings.MaxCount} 个");

        RuleForEach(x => x.Attachments).ChildRules(file =>
        {
            file.RuleFor(f => f)
                .Must(f => f!.Length > 0 && f.Length <= attachmentSettings.MaxSizeBytes)
                .WithMessage($"单个附件大小必须在 1 字节到 {attachmentSettings.MaxSizeBytes / 1024 / 1024}MB 之间");

            file.RuleFor(f => f)
                .Must(f => attachmentSettings.AllowedExtensions
                    .Contains(Path.GetExtension(f!.FileName).ToLowerInvariant()))
                .WithMessage($"附件类型不在允许范围内（允许：{string.Join("、", attachmentSettings.AllowedExtensions)}）");

            file.RuleFor(f => f)
                .Must(f => !string.IsNullOrWhiteSpace(f!.FileName) &&
                           !f.FileName.Contains('\\') && !f.FileName.Contains('/') && !f.FileName.Contains(".."))
                .WithMessage("附件文件名不合法");
        });
    }
}
