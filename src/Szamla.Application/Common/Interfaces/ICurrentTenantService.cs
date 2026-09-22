namespace Szamla.Application.Common.Interfaces;

/// <summary>
/// Resolves the tenant of the current request (from the JWT's tenant claim). Backs the EF Core
/// global query filters that keep tenants' data apart. When <see cref="HasTenant"/> is false,
/// tenant-scoped queries must return nothing rather than fall back to "no filter" — fail closed.
/// </summary>
public interface ICurrentTenantService
{
    Guid TenantId { get; }

    bool HasTenant { get; }
}
