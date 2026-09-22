using System.ComponentModel.DataAnnotations;

namespace Szamla.Api.Contracts.Auth;

public sealed record RegisterTenantRequest(
    [Required, MaxLength(200)] string CompanyName,
    [Required, MaxLength(20)] string TaxId,
    [Required, MaxLength(500)] string Address,
    [Required, MaxLength(200)] string OwnerFullName,
    [Required, EmailAddress, MaxLength(256)] string OwnerEmail,
    [Required, MinLength(10)] string OwnerPassword,
    string DefaultCurrency = "HUF");

public sealed record RegisterTenantResponse(Guid TenantId, Guid UserId);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

/// <summary>
/// Either the token pair (RequiresTwoFactor = false) or a short-lived TwoFactorToken the client
/// must present, together with the TOTP code, to POST /api/auth/2fa/verify.
/// </summary>
public sealed record LoginResponse(
    bool RequiresTwoFactor,
    string? TwoFactorToken,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? AccessTokenExpiresAtUtc);

public sealed record VerifyTwoFactorRequest(
    [Required] string TwoFactorToken,
    [Required, StringLength(6, MinimumLength = 6)] string Code);

public sealed record RefreshTokenRequest([Required] string RefreshToken);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc);

public sealed record EnableTwoFactorResponse(string SharedKey, string OtpAuthUri);

public sealed record ConfirmTwoFactorRequest([Required, StringLength(6, MinimumLength = 6)] string Code);
