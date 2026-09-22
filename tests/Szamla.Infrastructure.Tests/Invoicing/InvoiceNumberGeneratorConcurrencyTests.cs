using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Szamla.Domain.Invoices;
using Szamla.Domain.Tenants;
using Szamla.Infrastructure.Invoicing;
using Szamla.Infrastructure.Multitenancy;
using Szamla.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Szamla.Infrastructure.Tests.Invoicing;

/// <summary>
/// Proves the sequential-numbering guarantee the project brief requires: concurrent finalization
/// requests for the same tenant/series/year never collide and never skip a number. Requires
/// Docker (Testcontainers starts a real PostgreSQL instance).
/// </summary>
public class InvoiceNumberGeneratorConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("szamla_numbering_test")
        .WithUsername("szamla_numbering_test")
        .WithPassword("szamla_numbering_test")
        .Build();

    private Guid _tenantId;
    private Guid _seriesId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();

        var tenant = Tenant.Create("Konkurencia Teszt Kft.", "55566677-1-42", "cím");
        var series = InvoiceSeries.Create(tenant.Id, "SZ");
        dbContext.Tenants.Add(tenant);
        dbContext.InvoiceSeries.Add(series);
        await dbContext.SaveChangesAsync();

        _tenantId = tenant.Id;
        _seriesId = series.Id;
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
    public async Task NextAsync_CalledConcurrently_ProducesGaplessSequentialNumbersWithNoDuplicates()
    {
        const int concurrentCalls = 50;

        var tasks = Enumerable.Range(0, concurrentCalls).Select(async _ =>
        {
            // Each concurrent caller gets its own connection/DbContext, mirroring how two
            // simultaneous HTTP requests would each hold their own scoped ApplicationDbContext.
            await using var dbContext = CreateDbContext();
            var generator = new InvoiceNumberGenerator(dbContext);
            return await generator.NextAsync(_tenantId, _seriesId, 2026);
        });

        var numbers = await Task.WhenAll(tasks);

        numbers.Should().OnlyHaveUniqueItems();
        numbers.Order().Should().BeEquivalentTo(
            Enumerable.Range(1, concurrentCalls).Select(i => (long)i),
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task NextAsync_DifferentYearsOrSeries_CountIndependently()
    {
        await using var dbContext = CreateDbContext();
        var generator = new InvoiceNumberGenerator(dbContext);

        var firstOf2026 = await generator.NextAsync(_tenantId, _seriesId, 2026);
        var secondOf2026 = await generator.NextAsync(_tenantId, _seriesId, 2026);
        var firstOf2027 = await generator.NextAsync(_tenantId, _seriesId, 2027);

        firstOf2026.Should().Be(1);
        secondOf2026.Should().Be(2);
        firstOf2027.Should().Be(1); // a new year restarts the sequence for the same series
    }
}
