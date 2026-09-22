using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Szamla.Domain.Tenants;
using Szamla.Infrastructure.Auditing;
using Szamla.Infrastructure.Multitenancy;
using Szamla.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Szamla.Infrastructure.Tests.Auditing;

/// <summary>Proves every Added/Modified change to an audited entity produces a matching AuditLog row in the same SaveChanges call. Requires Docker.</summary>
public class AuditSaveChangesInterceptorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("szamla_audit_test")
        .WithUsername("szamla_audit_test")
        .WithPassword("szamla_audit_test")
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
            .AddInterceptors(new AuditSaveChangesInterceptor(new HttpContextAccessor())) // no ambient HttpContext: an anonymous/system action
            .Options;

        return new ApplicationDbContext(options, new NullCurrentTenantService());
    }

    [Fact]
    public async Task AddingATenant_WritesAnAddedAuditLogRowWithAfterStateAndNoBeforeState()
    {
        await using var dbContext = CreateDbContext();
        var tenant = Tenant.Create("Audit Teszt Kft.", "66677788-1-42", "cím");
        dbContext.Tenants.Add(tenant);

        await dbContext.SaveChangesAsync();

        var log = await dbContext.AuditLogs.SingleAsync(a => a.EntityType == nameof(Tenant) && a.EntityId == tenant.Id.ToString());
        log.Action.Should().Be("Added");
        log.BeforeState.Should().BeNull();
        log.AfterState.Should().NotBeNull();
        using var afterJson = JsonDocument.Parse(log.AfterState!);
        afterJson.RootElement.GetProperty("Name").GetString().Should().Be("Audit Teszt Kft.");
    }

    [Fact]
    public async Task ModifyingATenant_WritesAModifiedAuditLogRowWithBeforeAndAfterState()
    {
        var tenant = Tenant.Create("Régi Név Kft.", "77788899-1-42", "régi cím");
        await using (var setupContext = CreateDbContext())
        {
            setupContext.Tenants.Add(tenant);
            await setupContext.SaveChangesAsync();
        }

        await using var dbContext = CreateDbContext();
        var tracked = await dbContext.Tenants.SingleAsync(t => t.Id == tenant.Id);
        tracked.UpdateProfile("Új Név Kft.", "új cím", null, null);
        await dbContext.SaveChangesAsync();

        var log = await dbContext.AuditLogs
            .Where(a => a.EntityType == nameof(Tenant) && a.EntityId == tenant.Id.ToString() && a.Action == "Modified")
            .SingleAsync();

        using var beforeJson = JsonDocument.Parse(log.BeforeState!);
        using var afterJson = JsonDocument.Parse(log.AfterState!);
        beforeJson.RootElement.GetProperty("Name").GetString().Should().Be("Régi Név Kft.");
        afterJson.RootElement.GetProperty("Name").GetString().Should().Be("Új Név Kft.");
    }
}
