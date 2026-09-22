namespace Szamla.Application.Common.Interfaces;

/// <summary>
/// Issues JWTs for authenticated users. Takes primitive claims data rather than the Identity
/// ApplicationUser type, so Application does not need to depend on Infrastructure/Identity.
/// </summary>
public interface IJwtTokenGenerator
{
    string GenerateAccessToken(Guid userId, string email, Guid tenantId, IEnumerable<string> roles);
}
