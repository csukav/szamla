using Szamla.Application.Common.Interfaces;

namespace Szamla.Infrastructure.Multitenancy;

/// <summary>
/// Used where there is no HTTP request to derive a tenant from: EF Core design-time tooling
/// (migrations) and startup tasks such as role seeding that must bypass the per-tenant filter.
/// </summary>
public sealed class NullCurrentTenantService : ICurrentTenantService
{
    public Guid TenantId => Guid.Empty;

    public bool HasTenant => false;
}
