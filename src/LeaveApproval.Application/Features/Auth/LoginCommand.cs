using LeaveApproval.Application.DTOs;
using MediatR;

namespace LeaveApproval.Application.Features.Auth;

/// <summary>登录请求。</summary>
public record LoginCommand(string Username, string Password) : IRequest<LoginResponseDto>;

public record LoginResponseDto(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    UserSummaryDto User);
