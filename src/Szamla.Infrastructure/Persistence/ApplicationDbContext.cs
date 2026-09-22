using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Tenants;
using Szamla.Infrastructure.Identity;

namespace Szamla.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    // Accepted (not yet used) so tenant-scoped business entities added from Phase 3 on
    // (Partner, Product, Invoice, ...) can add HasQueryFilter(e => e.TenantId == currentTenantService.TenantId)
    // without another constructor change. See the comment on the ApplicationUser mapping below
    // for why Users itself is deliberately excluded from that pattern.
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentTenantService currentTenantService)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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
            // GET /api/users filters by the current tenant) — see UsersTenantIsolationTests.

            b.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
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
