using Microsoft.AspNetCore.Identity;

namespace Szamla.Infrastructure.Identity;

/// <summary>
/// The login for a person working within one tenant. Deliberately lives in Infrastructure, not
/// Domain: it is an ASP.NET Core Identity concept (password hash, lockout, 2FA secret), not a
/// business entity.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
