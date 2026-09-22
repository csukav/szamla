using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Invoices;
using Szamla.Domain.Tenants;
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
    }
}
