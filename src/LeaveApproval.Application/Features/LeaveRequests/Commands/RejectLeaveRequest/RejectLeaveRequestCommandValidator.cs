using FluentValidation;

namespace LeaveApproval.Application.Features.LeaveRequests.Commands;

public class RejectLeaveRequestCommandValidator : AbstractValidator<RejectLeaveRequestCommand>
{
    public RejectLeaveRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("请假单 ID 不合法");

        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("驳回时必须填写驳回意见")
            .MaximumLength(500).WithMessage("驳回意见长度不能超过 500 字");
    }
}
