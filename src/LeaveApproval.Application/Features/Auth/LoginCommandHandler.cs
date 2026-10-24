using LeaveApproval.Application.Common.Exceptions;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Application.DTOs;
using LeaveApproval.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LeaveApproval.Application.Features.Auth;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponseDto>
{
    private readonly IBaseRepository<User> _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        IBaseRepository<User> users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.Query()
            .AsNoTracking()
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        // 统一提示，避免用户枚举
        if (user == null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationFailedException("用户名或密码错误");
        }

        var token = _tokenService.CreateToken(user);

        return new LoginResponseDto(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            new UserSummaryDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role,
                DepartmentName = user.Department?.Name
            });
    }
}
