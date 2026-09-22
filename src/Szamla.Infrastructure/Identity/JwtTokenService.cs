using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Infrastructure.Identity;

/// <summary>
/// Issues and validates the JWTs used by the API: normal access tokens, and a narrow
/// "pending two-factor" token that only proves the password check passed, used to bridge
/// /auth/login and /auth/2fa/verify without server-side session state.
/// </summary>
public sealed class JwtTokenService : IJwtTokenGenerator
{
    private const string PendingTwoFactorClaimType = "amr";
    private const string PendingTwoFactorClaimValue = "mfa_pending";

    private readonly JwtSettings _settings;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_settings.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>The expiry an access token minted right now would carry — used by callers that need to report it alongside the token (e.g. the login response DTO).</summary>
    public DateTimeOffset GetAccessTokenExpiry() => DateTimeOffset.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

    public string GenerateAccessToken(Guid userId, string email, Guid tenantId, IEnumerable<string> roles)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", tenantId.ToString()),
        ];

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return CreateToken(claims, TimeSpan.FromMinutes(_settings.AccessTokenMinutes));
    }

    public string GeneratePendingTwoFactorToken(Guid userId)
    {
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(PendingTwoFactorClaimType, PendingTwoFactorClaimValue),
        ];

        return CreateToken(claims, TimeSpan.FromMinutes(_settings.TwoFactorPendingMinutes));
    }

    /// <summary>Returns the user id encoded in a pending-2FA token, or null if it is missing, expired, or not a pending-2FA token.</summary>
    public Guid? ValidatePendingTwoFactorToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, _validationParameters, out _);

            var isPending = principal.Claims.Any(c =>
                c.Type == PendingTwoFactorClaimType && c.Value == PendingTwoFactorClaimValue);
            if (!isPending)
            {
                return null;
            }

            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    private string CreateToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: _signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
