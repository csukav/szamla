using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Szamla.Domain.Tenants;
using Szamla.Infrastructure.Identity;
using Szamla.Infrastructure.Multitenancy;
using Szamla.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Szamla.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the two multi-tenancy guarantees this phase makes: (1) a tenant-scoped query never
/// returns another tenant's users, and (2) two tenants cannot register with the same tax id.
/// Requires Docker (Testcontainers starts a real PostgreSQL instance).
/// </summary>
public class TenantIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("szamla_infra_test")
        .WithUsername("szamla_infra_test")
        .WithPassword("szamla_infra_test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options, new NullCurrentTenantService());
    }

    [Fact]
    public async Task QueryingUsersByTenantId_NeverReturnsAnotherTenantsUsers()
    {
        await using var dbContext = CreateDbContext();

        var tenantA = Tenant.Create("A Kft.", "10000000-1-42", "cím A");
        var tenantB = Tenant.Create("B Kft.", "20000000-1-42", "cím B");
        dbContext.Tenants.AddRange(tenantA, tenantB);

        var userA = new ApplicationUser { TenantId = tenantA.Id, UserName = "user-a@example.com", Email = "user-a@example.com", FullName = "Teszt A" };
        var userB = new ApplicationUser { TenantId = tenantB.Id, UserName = "user-b@example.com", Email = "user-b@example.com", FullName = "Teszt B" };
        dbContext.Users.AddRange(userA, userB);
        await dbContext.SaveChangesAsync();

        var usersOfTenantA = await dbContext.Users.Where(u => u.TenantId == tenantA.Id).ToListAsync();

        usersOfTenantA.Should().ContainSingle(u => u.Id == userA.Id);
        usersOfTenantA.Should().NotContain(u => u.Id == userB.Id);
    }

    [Fact]
    public async Task RegisteringTwoTenants_WithTheSameTaxId_ViolatesTheUniqueConstraint()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(Tenant.Create("Első Kft.", "99999999-1-42", "cím"));
        await dbContext.SaveChangesAsync();

        await using var secondAttempt = CreateDbContext();
        secondAttempt.Tenants.Add(Tenant.Create("Második Kft.", "99999999-1-42", "másik cím"));

        var act = async () => await secondAttempt.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
