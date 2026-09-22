using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Common;
using Szamla.Domain.Invoices;
using Szamla.Domain.Partners;
using Szamla.Domain.Tenants;
using Szamla.Infrastructure.Multitenancy;
using Szamla.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Szamla.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the Invoice/InvoiceLine/Partner EF Core mapping actually round-trips through real
/// PostgreSQL (compiling is not enough — ComplexProperty and the InvoiceLine-as-separate-entity
/// workaround are exercised here), that the tenant query filter genuinely scopes results, and
/// that duplicate invoice numbers are rejected at the database level, not just by Domain-layer
/// checks. Requires Docker.
/// </summary>
public class InvoicePersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("szamla_invoice_test")
        .WithUsername("szamla_invoice_test")
        .WithPassword("szamla_invoice_test")
        .Build();

    // Factory methods, not shared static instances: EF Core's complex-property change tracking
    // got confused when the exact same IssuerSnapshot/PartnerSnapshot reference was attached to
    // two different Invoice entities in the same SaveChanges call (one insert would go through
    // with null columns) — each invoice gets its own instance instead.
    private static IssuerSnapshot NewIssuer() => new("Kiállító Kft.", "12345678-1-42", "cím", null, false);

    private static PartnerSnapshot NewBuyer() => new("Vevő Kft.", false, PartnerCountryCategory.Domestic, "HU", "cím", "87654321-1-42", null, null);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var dbContext = CreateDbContext(new NullCurrentTenantService());
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ApplicationDbContext CreateDbContext(ICurrentTenantService currentTenantService)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options, currentTenantService);
    }

    private static InvoiceLine[] OneHufLine() =>
        [InvoiceLine.Create("Tétel", 1m, "db", new Money(1000m, "HUF"), VatRate.OfPercentage(0.27m))];

    [Fact]
    public async Task SavingAndReloadingAnInvoice_PreservesLinesTotalsAndSnapshots()
    {
        var tenant = Tenant.Create("Kiállító Kft.", "12345678-1-42", "cím");

        await using (var writeContext = CreateDbContext(new NullCurrentTenantService()))
        {
            writeContext.Tenants.Add(tenant);

            var lines = new[]
            {
                InvoiceLine.Create("Tanácsadás", 2m, "óra", new Money(10000m, "HUF"), VatRate.OfPercentage(0.27m)),
                InvoiceLine.Create("Oktatás", 1m, "db", new Money(5000m, "HUF"), VatRate.Exempt(VatExemptionReason.SubjectExempt)),
            };
            var invoice = Invoice.CreateDraft(
                tenant.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 9),
                PaymentMethod.BankTransfer, "HUF", NewIssuer(), NewBuyer(), lines);
            writeContext.Invoices.Add(invoice);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateDbContext(new FixedCurrentTenantService(tenant.Id));
        var reloaded = await readContext.Invoices
            .Include(i => i.Lines)
            .SingleAsync();

        reloaded.Lines.Should().HaveCount(2);
        reloaded.NetTotal.Should().Be(new Money(25000m, "HUF"));
        reloaded.VatTotal.Should().Be(new Money(5400m, "HUF"));
        reloaded.Issuer.Should().Be(NewIssuer());
        reloaded.Partner.Should().Be(NewBuyer());
        reloaded.Lines.Should().Contain(l => l.Description == "Tanácsadás" && l.VatRate.Equals(VatRate.OfPercentage(0.27m)));
        reloaded.Lines.Should().Contain(l => l.Description == "Oktatás" && l.VatRate.Kind == VatRateKind.Exempt);
    }

    [Fact]
    public async Task TwoInvoices_WithTheSameTenantAndNumber_ViolateTheUniqueConstraint()
    {
        var tenant = Tenant.Create("Kiállító Kft.", "22233344-1-42", "cím");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using (var setupContext = CreateDbContext(new NullCurrentTenantService()))
        {
            setupContext.Tenants.Add(tenant);
            await setupContext.SaveChangesAsync();
        }

        await using (var firstContext = CreateDbContext(new NullCurrentTenantService()))
        {
            var first = Invoice.CreateDraft(tenant.Id, today, today, today, PaymentMethod.Cash, "HUF", NewIssuer(), NewBuyer(), OneHufLine());
            first.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow);
            firstContext.Invoices.Add(first);
            await firstContext.SaveChangesAsync();
        }

        await using var secondContext = CreateDbContext(new NullCurrentTenantService());
        var second = Invoice.CreateDraft(tenant.Id, today, today, today, PaymentMethod.Cash, "HUF", NewIssuer(), NewBuyer(), OneHufLine());
        second.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow); // same number, deliberately
        secondContext.Invoices.Add(second);

        var act = async () => await secondContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task QueryingInvoices_AsOneTenant_NeverReturnsAnotherTenantsInvoices()
    {
        var tenantA = Tenant.Create("A Kft.", "33344455-1-42", "cím A");
        var tenantB = Tenant.Create("B Kft.", "44455566-1-42", "cím B");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Invoice invoiceA;
        Invoice invoiceB;
        await using (var writeContext = CreateDbContext(new NullCurrentTenantService()))
        {
            writeContext.Tenants.AddRange(tenantA, tenantB);

            invoiceA = Invoice.CreateDraft(tenantA.Id, today, today, today, PaymentMethod.Cash, "HUF", NewIssuer(), NewBuyer(), OneHufLine());
            invoiceB = Invoice.CreateDraft(tenantB.Id, today, today, today, PaymentMethod.Cash, "HUF", NewIssuer(), NewBuyer(), OneHufLine());
            writeContext.Invoices.AddRange(invoiceA, invoiceB);
            await writeContext.SaveChangesAsync();
        }

        // A query scoped as tenant A's own request — no explicit Where needed, the global filter
        // (HasQueryFilter in ApplicationDbContext) does the isolation on its own.
        await using var tenantAContext = CreateDbContext(new FixedCurrentTenantService(tenantA.Id));
        var invoicesVisibleToTenantA = await tenantAContext.Invoices.ToListAsync();

        invoicesVisibleToTenantA.Should().ContainSingle(i => i.Id == invoiceA.Id);
        invoicesVisibleToTenantA.Should().NotContain(i => i.Id == invoiceB.Id);
    }

    private sealed class FixedCurrentTenantService(Guid tenantId) : ICurrentTenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool HasTenant => true;
    }
}
