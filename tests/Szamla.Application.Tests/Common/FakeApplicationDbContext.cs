using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Common;
using Szamla.Domain.Invoices;
using Szamla.Domain.Partners;
using Szamla.Domain.Tenants;

namespace Szamla.Application.Tests.Common;

/// <summary>
/// A real EF Core DbContext backed by the InMemory provider, used purely so Application-layer
/// handler tests can exercise IApplicationDbContext without depending on Szamla.Infrastructure
/// (which would pull in PostgreSQL/Identity and break layering). The model configuration here is
/// a deliberately trimmed-down echo of Szamla.Infrastructure.Persistence.ApplicationDbContext —
/// just enough for InMemory to materialize these types, not a source of truth on its own; the
/// real schema (and its constraints, like the unique invoice number index) is only proven by the
/// Testcontainers-backed tests in Szamla.Infrastructure.Tests.
/// </summary>
public sealed class FakeApplicationDbContext : DbContext, IApplicationDbContext
{
    public FakeApplicationDbContext(DbContextOptions<FakeApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Partner> Partners => Set<Partner>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<InvoiceSeries> InvoiceSeries => Set<InvoiceSeries>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Invoice>(b =>
        {
            b.Ignore(i => i.VatSummary);
            b.HasMany(i => i.Lines).WithOne().HasForeignKey("InvoiceId");
            b.OwnsOne(i => i.Issuer);
            b.OwnsOne(i => i.Partner);
            b.ComplexProperty(i => i.NetTotal, ConfigureMoney);
            b.ComplexProperty(i => i.VatTotal, ConfigureMoney);
            b.ComplexProperty(i => i.GrossTotal, ConfigureMoney);
        });

        builder.Entity<InvoiceLine>(b =>
        {
            b.Property<Guid>("InvoiceId");
            b.ComplexProperty(l => l.NetUnitPrice, ConfigureMoney);
            b.ComplexProperty(l => l.NetAmount, ConfigureMoney);
            b.ComplexProperty(l => l.VatAmount, ConfigureMoney);
            b.ComplexProperty(l => l.GrossAmount, ConfigureMoney);
            b.ComplexProperty(l => l.VatRate, vatRate =>
            {
                vatRate.Property(x => x.Kind);
                vatRate.Property(x => x.Percentage);
                vatRate.Property(x => x.ExemptionReason);
            });
        });
    }

    // Explicit Property() calls (matching Szamla.Infrastructure.Persistence.ApplicationDbContext)
    // are required, not optional polish: without them EF falls back to constructor-binding the
    // complex type, and that fails outright for Money (see the ComplexProperty comment there).
    private static void ConfigureMoney(ComplexPropertyBuilder<Money> money)
    {
        money.Property(x => x.Amount);
        money.Property(x => x.CurrencyCode);
    }

    public static FakeApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<FakeApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FakeApplicationDbContext(options);
    }
}
