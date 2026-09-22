using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Interfaces;
using Szamla.Application.Invoices.Commands;
using Szamla.Application.Invoices.Common;
using Szamla.Application.Partners.Commands;
using Szamla.Domain.Invoices;
using Szamla.Domain.Partners;
using Szamla.Domain.Tenants;
using Szamla.Infrastructure.Invoicing;
using Szamla.Infrastructure.Multitenancy;
using Szamla.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Szamla.Infrastructure.Tests.Invoices;

/// <summary>
/// End-to-end test of the Application-layer command handlers driving a full invoice lifecycle
/// against real PostgreSQL: draft -> edit lines -> finalize -> storno -> finalize the storno.
/// Each command gets its own fresh ApplicationDbContext, deliberately — that's what a real
/// request-scoped IApplicationDbContext looks like, and reusing one context across handler calls
/// (each of which queries-then-saves) hits EF Core's identity-map/change-tracking edge cases
/// that a real request never encounters. Requires Docker.
/// </summary>
public class InvoiceLifecycleHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("szamla_lifecycle_test")
        .WithUsername("szamla_lifecycle_test")
        .WithPassword("szamla_lifecycle_test")
        .Build();

    private DbContextOptions<ApplicationDbContext> _options = default!;
    private Guid _tenantId;
    private Guid _partnerId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        // Tenant creation itself precedes any tenant context (nothing to scope to yet), same as
        // RegisterTenantCommand in the real API.
        await using (var setupContext = new ApplicationDbContext(_options, new NullCurrentTenantService()))
        {
            await setupContext.Database.MigrateAsync();
            var tenant = Tenant.Create("Életciklus Teszt Kft.", "88899900-1-42", "cím");
            setupContext.Tenants.Add(tenant);
            await setupContext.SaveChangesAsync();
            _tenantId = tenant.Id;
        }

        await using var partnerContext = NewTenantScopedDbContext();
        _partnerId = await new CreatePartnerCommandHandler(partnerContext).Handle(
            new CreatePartnerCommand(_tenantId, "Vevő Kft.", false, PartnerCountryCategory.Domestic, "HU", "vevő cím", TaxId: "11122233-1-42"),
            CancellationToken.None);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ApplicationDbContext NewTenantScopedDbContext() => new(_options, new FixedCurrentTenantService(_tenantId));

    private static InvoiceLineInput OneLine(decimal netUnitPrice = 10000m) =>
        new("Tétel", 1m, "db", netUnitPrice, new VatRateInput(VatRateKind.Percentage, 0.27m, null));

    [Fact]
    public async Task FullLifecycle_DraftEditFinalizeStornoFinalize_ProducesTwoSequentialCancellingInvoices()
    {
        await using var seriesContext = NewTenantScopedDbContext();
        var seriesId = await new CreateInvoiceSeriesCommandHandler(seriesContext).Handle(
            new CreateInvoiceSeriesCommand(_tenantId, "SZ"), CancellationToken.None);

        var issueDate = new DateOnly(2026, 4, 1);
        await using var createContext = NewTenantScopedDbContext();
        var invoiceId = await new CreateDraftInvoiceCommandHandler(createContext).Handle(
            new CreateDraftInvoiceCommand(_tenantId, _partnerId, issueDate, issueDate, issueDate.AddDays(8),
                PaymentMethod.BankTransfer, "HUF", [OneLine()]),
            CancellationToken.None);

        // Edit the draft before finalizing.
        await using var editContext = NewTenantScopedDbContext();
        await new ReplaceDraftInvoiceLinesCommandHandler(editContext).Handle(
            new ReplaceDraftInvoiceLinesCommand(_tenantId, invoiceId, [OneLine(20000m)]),
            CancellationToken.None);

        await using var finalizeContext = NewTenantScopedDbContext();
        var number = await new FinalizeInvoiceCommandHandler(finalizeContext, new InvoiceNumberGenerator(finalizeContext)).Handle(
            new FinalizeInvoiceCommand(_tenantId, invoiceId, seriesId), CancellationToken.None);

        number.Should().Be("SZ-2026-000001");

        await using var stornoContext = NewTenantScopedDbContext();
        var stornoId = await new CreateStornoInvoiceCommandHandler(stornoContext).Handle(
            new CreateStornoInvoiceCommand(_tenantId, invoiceId, issueDate.AddDays(1)), CancellationToken.None);

        await using var finalizeStornoContext = NewTenantScopedDbContext();
        var stornoNumber = await new FinalizeInvoiceCommandHandler(finalizeStornoContext, new InvoiceNumberGenerator(finalizeStornoContext)).Handle(
            new FinalizeInvoiceCommand(_tenantId, stornoId, seriesId), CancellationToken.None);

        stornoNumber.Should().Be("SZ-2026-000002");

        await using var verifyContext = NewTenantScopedDbContext();
        var original = await verifyContext.Invoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoiceId);
        var storno = await verifyContext.Invoices.Include(i => i.Lines).SingleAsync(i => i.Id == stornoId);

        original.Status.Should().Be(InvoiceStatus.Finalized);
        original.NetTotal.Amount.Should().Be(20000m);
        storno.Type.Should().Be(InvoiceType.Storno);
        storno.OriginalInvoiceId.Should().Be(invoiceId);
        (original.GrossTotal + storno.GrossTotal).Amount.Should().Be(0m);
    }

    [Fact]
    public async Task ReplacingLines_OnAFinalizedInvoice_ThrowsInvalidOperationException()
    {
        await using var seriesContext = NewTenantScopedDbContext();
        var seriesId = await new CreateInvoiceSeriesCommandHandler(seriesContext).Handle(
            new CreateInvoiceSeriesCommand(_tenantId, "SZ2"), CancellationToken.None);

        var issueDate = new DateOnly(2026, 4, 1);
        await using var createContext = NewTenantScopedDbContext();
        var invoiceId = await new CreateDraftInvoiceCommandHandler(createContext).Handle(
            new CreateDraftInvoiceCommand(_tenantId, _partnerId, issueDate, issueDate, issueDate.AddDays(8), PaymentMethod.Cash, "HUF", [OneLine()]),
            CancellationToken.None);

        await using var finalizeContext = NewTenantScopedDbContext();
        await new FinalizeInvoiceCommandHandler(finalizeContext, new InvoiceNumberGenerator(finalizeContext)).Handle(
            new FinalizeInvoiceCommand(_tenantId, invoiceId, seriesId), CancellationToken.None);

        await using var editContext = NewTenantScopedDbContext();
        var act = () => new ReplaceDraftInvoiceLinesCommandHandler(editContext).Handle(
            new ReplaceDraftInvoiceLinesCommand(_tenantId, invoiceId, [OneLine()]), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class FixedCurrentTenantService(Guid tenantId) : ICurrentTenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool HasTenant => true;
    }
}
