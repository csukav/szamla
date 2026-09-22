namespace Szamla.Infrastructure.Identity;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = default!;

    public string Audience { get; set; } = default!;

    /// <summary>Base64-encoded symmetric signing key. Minimum 256 bits (32 bytes) once decoded.</summary>
    public string SigningKey { get; set; } = default!;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>How long a "pending 2FA" token (issued after password check, before the TOTP code) stays valid.</summary>
    public int TwoFactorPendingMinutes { get; set; } = 5;
}
