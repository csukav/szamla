using Microsoft.AspNetCore.Http;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Infrastructure.Multitenancy;

/// <summary>
/// Reads the tenant id from the current HTTP request's "tenant_id" claim. Registered per-request
/// (scoped), so the EF Core global query filter it backs always reflects the caller who is
/// actually making the request.
/// </summary>
public sealed class CurrentTenantService : ICurrentTenantService
{
    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        var claimValue = httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;

        if (Guid.TryParse(claimValue, out var tenantId))
        {
            TenantId = tenantId;
            HasTenant = true;
        }
    }

    public Guid TenantId { get; } = Guid.Empty;

    public bool HasTenant { get; }
}
