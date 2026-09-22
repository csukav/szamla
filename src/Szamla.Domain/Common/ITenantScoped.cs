namespace Szamla.Domain.Common;

/// <summary>
/// Marker for entities that belong to exactly one tenant. Implementations must never be
/// queried without a tenant filter applied (see the EF Core global query filters in
/// Szamla.Infrastructure).
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}
