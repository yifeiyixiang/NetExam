using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LeaveApproval.Application.Common.Interfaces;
using LeaveApproval.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LeaveApproval.Infrastructure.Security;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "LeaveApproval.Api";

    public string Audience { get; set; } = "LeaveApproval.Client";

    /// <summary>签名密钥，至少 32 字符。生产环境通过环境变量 Jwt__SecretKey 注入，不得提交到 Git。</summary>
    public string SecretKey { get; set; } = string.Empty;

    public int ExpiresInMinutes { get; set; } = 480;
}

/// <summary>JWT 签发：包含 sub（用户 ID）、name、role、jti。</summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public JwtTokenResult CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("name", user.Username),
            new("role", user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_settings.ExpiresInMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new JwtTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

/// <summary>BCrypt 密码哈希（自动盐），不存明文。</summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
