using LeaveApproval.Domain.Entities;

namespace LeaveApproval.Application.Common.Interfaces;

public record JwtTokenResult(string AccessToken, DateTime ExpiresAtUtc);

public interface ITokenService
{
    /// <summary>为用户签发 JWT（包含 sub/name/role/jti）。</summary>
    JwtTokenResult CreateToken(User user);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
