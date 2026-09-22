using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Invoices;
using Szamla.Domain.Partners;
using Szamla.Domain.Tenants;
using Szamla.Infrastructure.Auditing;
using Szamla.Infrastructure.Identity;
using Szamla.Infrastructure.Invoicing;

namespace Szamla.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private readonly ICurrentTenantService _currentTenantService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentTenantService currentTenantService)
        : base(options)
    {
        _currentTenantService = currentTenantService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<InvoiceSeries> InvoiceSeries => Set<InvoiceSeries>();

    public DbSet<Partner> Partners => Set<Partner>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).HasMaxLength(200).IsRequired();
            b.Property(t => t.TaxId).HasMaxLength(20).IsRequired();
            b.HasIndex(t => t.TaxId).IsUnique();
            b.Property(t => t.Address).HasMaxLength(500).IsRequired();
            b.Property(t => t.BankAccount).HasMaxLength(100);
            b.Property(t => t.LogoUrl).HasMaxLength(1000);
            b.Property(t => t.DefaultCurrency).HasMaxLength(3).IsRequired();
        });

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("Users");
            b.Property(u => u.TenantId).IsRequired();
            b.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            b.HasIndex(u => u.TenantId);

            // Deliberately NOT tenant-filtered, unlike the business entities from Phase 3 on
            // (Partner, Product, Invoice, ...). Login identifies a user by email alone, before
            // any tenant is known, and ASP.NET Core Identity's own RequireUniqueEmail check runs
            // through this same DbSet — both would silently break under a global tenant filter
            // (login would find nobody; uniqueness would only be enforced within Guid.Empty).
            // Tenant isolation for Users is instead enforced explicitly at the call site (e.g.
            // GET /api/users filters by the current tenant) — see TenantIsolationTests.

            b.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceSeries>(b =>
        {
            b.ToTable("InvoiceSeries");
            b.HasKey(s => s.Id);
            b.Property(s => s.Prefix).HasMaxLength(10).IsRequired();
            b.HasIndex(s => new { s.TenantId, s.Prefix }).IsUnique();
            b.HasQueryFilter(s => s.TenantId == _currentTenantService.TenantId);
        });

        builder.Entity<InvoiceNumberCounter>(b =>
        {
            b.ToTable("InvoiceNumberCounters");
            b.HasKey(c => new { c.TenantId, c.SeriesId, c.Year });

            // No tenant query filter here, deliberately: this table is only ever touched through
            // the raw atomic UPSERT in InvoiceNumberGenerator, never via LINQ, so a filter here
            // would just be dead configuration.
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("RefreshTokens");
            b.HasKey(r => r.Id);
            b.Property(r => r.TokenHash).HasMaxLength(200).IsRequired();
            b.HasIndex(r => r.TokenHash).IsUnique();
            b.HasIndex(r => r.UserId);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Rename the remaining default ASP.NET Core Identity tables away from the AspNet* prefix.
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        ConfigurePartner(builder);
        ConfigureInvoice(builder);
        ConfigureAuditLog(builder);
    }

    private static void ConfigureAuditLog(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLogs");
            b.HasKey(a => a.Id);
            b.Property(a => a.Action).HasMaxLength(20).IsRequired();
            b.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
            b.Property(a => a.EntityId).HasMaxLength(200).IsRequired();
            b.Property(a => a.IpAddress).HasMaxLength(45); // IPv6 worst case
            b.HasIndex(a => new { a.EntityType, a.EntityId });
            b.HasIndex(a => a.TenantId);

            // No tenant query filter, deliberately: nothing queries AuditLogs through this
            // DbContext yet (a scoped GET /api/audit-log endpoint is future work), and once it
            // exists it needs to explicitly decide who may see cross-tenant entries (e.g. an
            // internal support/ops role) rather than inherit the ordinary per-request tenant filter.
        });
    }

    private void ConfigurePartner(ModelBuilder builder)
    {
        builder.Entity<Partner>(b =>
        {
            b.ToTable("Partners");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.CountryCode).HasMaxLength(2).IsRequired();
            b.Property(p => p.TaxId).HasMaxLength(20);
            b.Property(p => p.EuVatId).HasMaxLength(20);
            b.Property(p => p.Address).HasMaxLength(500).IsRequired();
            b.Property(p => p.Email).HasMaxLength(256);
            b.HasIndex(p => p.TenantId);
            b.HasQueryFilter(p => p.TenantId == _currentTenantService.TenantId);
        });
    }

    private void ConfigureInvoice(ModelBuilder builder)
    {
        builder.Entity<Invoice>(b =>
        {
            b.ToTable("Invoices");
            b.HasKey(i => i.Id);
            b.Property(i => i.Number).HasMaxLength(50);
            b.Property(i => i.Currency).HasMaxLength(3).IsRequired();
            b.Property(i => i.ExchangeRate).HasPrecision(18, 6);
            b.Property(i => i.ExchangeRateSource).HasMaxLength(100);
            b.Property(i => i.VatTotalHufAmount).HasPrecision(18, 2);
            b.HasIndex(i => i.TenantId);
            b.HasIndex(i => i.OriginalInvoiceId);
            // Postgres treats each NULL as distinct in a unique index, so any number of Draft
            // invoices (Number == null) coexist fine — only two rows that both have the *same*
            // non-null Number would violate this, which is exactly the gaplessness guarantee.
            b.HasIndex(i => new { i.TenantId, i.Number }).IsUnique();
            b.HasQueryFilter(i => i.TenantId == _currentTenantService.TenantId);

            // Purely computed from Lines (see the doc comment on Invoice.VatSummary) — not a
            // real column, and without this EF's convention-based discovery tries to map it as
            // an owned collection just because it's a public property.
            b.Ignore(i => i.VatSummary);

            b.OwnsOne(i => i.Issuer, issuer =>
            {
                issuer.Property(x => x.Name).HasColumnName("IssuerName").HasMaxLength(200).IsRequired();
                issuer.Property(x => x.TaxId).HasColumnName("IssuerTaxId").HasMaxLength(20).IsRequired();
                issuer.Property(x => x.Address).HasColumnName("IssuerAddress").HasMaxLength(500).IsRequired();
                issuer.Property(x => x.BankAccount).HasColumnName("IssuerBankAccount").HasMaxLength(100);
                issuer.Property(x => x.IsVatExempt).HasColumnName("IssuerIsVatExempt");
            });

            b.OwnsOne(i => i.Partner, partner =>
            {
                partner.Property(x => x.Name).HasColumnName("PartnerName").HasMaxLength(200).IsRequired();
                partner.Property(x => x.IsPrivatePerson).HasColumnName("PartnerIsPrivatePerson");
                partner.Property(x => x.CountryCategory).HasColumnName("PartnerCountryCategory");
                partner.Property(x => x.CountryCode).HasColumnName("PartnerCountryCode").HasMaxLength(2).IsRequired();
                partner.Property(x => x.Address).HasColumnName("PartnerAddress").HasMaxLength(500).IsRequired();
                partner.Property(x => x.TaxId).HasColumnName("PartnerTaxId").HasMaxLength(20);
                partner.Property(x => x.EuVatId).HasColumnName("PartnerEuVatId").HasMaxLength(20);
                partner.Property(x => x.Email).HasColumnName("PartnerEmail").HasMaxLength(256);
            });

            // Money is a struct (value semantics matter for its equality/arithmetic), and EF
            // Core's OwnsOne/OwnsMany require a reference type — so every Money-typed property
            // is mapped via ComplexProperty instead, the EF8+ feature built for exactly this case.
            b.ComplexProperty(i => i.NetTotal, m =>
            {
                m.Property(x => x.Amount).HasColumnName("NetTotalAmount").HasPrecision(18, 2);
                m.Property(x => x.CurrencyCode).HasColumnName("NetTotalCurrency").HasMaxLength(3).IsRequired();
            });
            b.ComplexProperty(i => i.VatTotal, m =>
            {
                m.Property(x => x.Amount).HasColumnName("VatTotalAmount").HasPrecision(18, 2);
                m.Property(x => x.CurrencyCode).HasColumnName("VatTotalCurrency").HasMaxLength(3).IsRequired();
            });
            b.ComplexProperty(i => i.GrossTotal, m =>
            {
                m.Property(x => x.Amount).HasColumnName("GrossTotalAmount").HasPrecision(18, 2);
                m.Property(x => x.CurrencyCode).HasColumnName("GrossTotalCurrency").HasMaxLength(3).IsRequired();
            });

            // Lines is a read-only view (`=> _lines`) over the private backing field, by design —
            // see the comment on Invoice.Lines. EF Core's backing-field convention picks up
            // "_lines" for "Lines" automatically, so writes go through the field, never the
            // (nonexistent) setter.
            b.HasMany(i => i.Lines).WithOne().HasForeignKey("InvoiceId").OnDelete(DeleteBehavior.Cascade);
        });

        // InvoiceLine is mapped as a regular entity with a shadow FK back to Invoice, not as an
        // EF Core "owned type" (OwnsMany): owned types can't currently contain ComplexProperty
        // mappings, which every Money-typed property here needs (see the ComplexProperty comment
        // on Invoice above). InvoiceLine already has its own Id, so modeling it as a normal
        // entity — reachable only through Invoice.Lines in practice, never queried on its own —
        // isn't a meaningful loss of encapsulation.
        builder.Entity<InvoiceLine>(b =>
        {
            b.ToTable("InvoiceLines");
            b.HasKey(l => l.Id);
            b.Property(l => l.Description).HasMaxLength(500).IsRequired();
            b.Property(l => l.Quantity).HasPrecision(18, 3);
            b.Property(l => l.Unit).HasMaxLength(50).IsRequired();
            b.Property<Guid>("InvoiceId");
            b.HasIndex("InvoiceId");

            b.ComplexProperty(l => l.NetUnitPrice, m =>
            {
                m.Property(x => x.Amount).HasColumnName("NetUnitPriceAmount").HasPrecision(18, 2);
                m.Property(x => x.CurrencyCode).HasColumnName("NetUnitPriceCurrency").HasMaxLength(3).IsRequired();
            });
            b.ComplexProperty(l => l.NetAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("NetAmount").HasPrecision(18, 2);
                m.Property(x => x.CurrencyCode).HasColumnName("NetAmountCurrency").HasMaxLength(3).IsRequired();
            });
            b.ComplexProperty(l => l.VatAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("VatAmount").HasPrecision(18, 2);
                m.Property(x => x.CurrencyCode).HasColumnName("VatAmountCurrency").HasMaxLength(3).IsRequired();
            });
            b.ComplexProperty(l => l.GrossAmount, m =>
            {
                m.Property(x => x.Amount).HasColumnName("GrossAmount").HasPrecision(18, 2);
                m.Property(x => x.CurrencyCode).HasColumnName("GrossAmountCurrency").HasMaxLength(3).IsRequired();
            });

            b.ComplexProperty(l => l.VatRate, vatRate =>
            {
                vatRate.Property(x => x.Kind).HasColumnName("VatRateKind");
                vatRate.Property(x => x.Percentage).HasColumnName("VatRatePercentage").HasPrecision(5, 4);
                vatRate.Property(x => x.ExemptionReason).HasColumnName("VatExemptionReason");
            });
        });
    }
}
