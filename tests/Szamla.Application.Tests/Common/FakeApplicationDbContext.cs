using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Tenants;

namespace Szamla.Application.Tests.Common;

/// <summary>
/// A real EF Core DbContext backed by the InMemory provider, used purely so Application-layer
/// handler tests can exercise IApplicationDbContext without depending on Szamla.Infrastructure
/// (which would pull in PostgreSQL/Identity and break layering).
/// </summary>
public sealed class FakeApplicationDbContext : DbContext, IApplicationDbContext
{
    public FakeApplicationDbContext(DbContextOptions<FakeApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public static FakeApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<FakeApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FakeApplicationDbContext(options);
    }
}
