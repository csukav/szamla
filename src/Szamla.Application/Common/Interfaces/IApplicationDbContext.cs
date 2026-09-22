using Microsoft.EntityFrameworkCore;
using Szamla.Domain.Tenants;

namespace Szamla.Application.Common.Interfaces;

/// <summary>
/// The persistence surface the Application layer is allowed to see. Implemented by
/// Szamla.Infrastructure's ApplicationDbContext; keeps Application free of EF Core's
/// provider-specific and Identity-specific concerns.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
