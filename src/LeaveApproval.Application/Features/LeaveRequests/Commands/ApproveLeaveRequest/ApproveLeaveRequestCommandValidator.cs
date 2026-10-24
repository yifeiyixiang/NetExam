using FluentValidation;

namespace LeaveApproval.Application.Features.LeaveRequests.Commands;

public class ApproveLeaveRequestCommandValidator : AbstractValidator<ApproveLeaveRequestCommand>
{
    public ApproveLeaveRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("请假单 ID 不合法");

        RuleFor(x => x.Comment)
            .MaximumLength(500).WithMessage("审批意见长度不能超过 500 字");
    }
}
