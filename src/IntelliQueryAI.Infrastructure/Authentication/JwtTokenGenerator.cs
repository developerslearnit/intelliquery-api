using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IntelliQueryAI.Infrastructure.Authentication;

/// <summary>
/// Issues signed JWT tokens using HMAC-SHA256 and generates secure refresh tokens.
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateAccessToken(User user, Workspace? currentWorkspace = null, string? workspaceRole = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("system_role", user.SystemRole.ToString()),
            new(ClaimTypes.Role, user.SystemRole.ToString())
        };

        if (currentWorkspace is not null)
        {
            claims.Add(new Claim("tenant_id", currentWorkspace.Id.ToString()));
            claims.Add(new Claim("tenant_slug", currentWorkspace.Slug.Value));
            if (!string.IsNullOrWhiteSpace(workspaceRole))
            {
                claims.Add(new Claim("workspace_role", workspaceRole));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
