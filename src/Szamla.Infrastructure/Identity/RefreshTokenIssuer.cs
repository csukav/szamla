using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Szamla.Infrastructure.Persistence;

namespace Szamla.Infrastructure.Identity;

/// <summary>
/// Issues and rotates refresh tokens. The raw token is returned to the caller once and only its
/// SHA-256 hash is persisted (see RefreshToken.TokenHash).
/// </summary>
public sealed class RefreshTokenIssuer(ApplicationDbContext dbContext, IOptions<JwtSettings> jwtSettings)
{
    private readonly JwtSettings _settings = jwtSettings.Value;

    public async Task<(string RawToken, RefreshToken Entity)> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rawToken = GenerateRawToken();
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(_settings.RefreshTokenDays),
        };

        dbContext.RefreshTokens.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (rawToken, entity);
    }

    /// <summary>Validates a raw refresh token, revokes it (single use), and returns the owning user id — or null if it was invalid, expired, or already used.</summary>
    public async Task<Guid?> ConsumeAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var entity = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (entity is null || !entity.IsActive)
        {
            return null;
        }

        entity.RevokedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.UserId;
    }

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
