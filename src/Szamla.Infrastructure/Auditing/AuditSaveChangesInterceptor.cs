using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Szamla.Domain.Invoices;
using Szamla.Domain.Partners;
using Szamla.Domain.Tenants;

namespace Szamla.Infrastructure.Auditing;

/// <summary>
/// Writes one AuditLog row per Added/Modified/Deleted change to a business entity, as part of
/// the same SaveChanges call that makes the change — so there is no window where a change is
/// persisted without its audit trail. Scoped to the entity types actually worth auditing
/// (Tenant, Partner, Invoice, InvoiceSeries); ASP.NET Core Identity's own tables (Users,
/// RefreshTokens, ...) are excluded, both because they're not "business data" in the sense the
/// project brief means and because their volume (every login rotates a RefreshToken row) would
/// swamp the log with noise.
///
/// Captures only top-level scalar properties, not nested complex-type members (Money, VatRate,
/// the Issuer/Partner snapshots) — good enough to see e.g. an Invoice's Status/Number change,
/// not a fillér-level diff of its totals. Revisit if a real audit review ever needs that detail.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private static readonly Type[] AuditedTypes = [typeof(Tenant), typeof(Partner), typeof(Invoice), typeof(InvoiceSeries)];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AddAuditEntries(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            AddAuditEntries(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void AddAuditEntries(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => AuditedTypes.Contains(e.Entity.GetType()) && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var user = httpContextAccessor.HttpContext?.User;
        var userId = ParseGuidClaim(user, ClaimTypes.NameIdentifier) ?? ParseGuidClaim(user, "sub");
        var tenantIdFromClaim = ParseGuidClaim(user, "tenant_id");
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        foreach (var entry in entries)
        {
            context.Set<AuditLog>().Add(new AuditLog
            {
                TenantId = tenantIdFromClaim ?? TryGetTenantIdProperty(entry),
                UserId = userId,
                Action = entry.State.ToString(),
                EntityType = entry.Entity.GetType().Name,
                EntityId = GetPrimaryKeyValue(entry),
                BeforeState = entry.State == EntityState.Added ? null : Serialize(entry, useOriginalValues: true),
                AfterState = entry.State == EntityState.Deleted ? null : Serialize(entry, useOriginalValues: false),
                IpAddress = ipAddress,
            });
        }
    }

    private static Guid? ParseGuidClaim(ClaimsPrincipal? user, string claimType) =>
        Guid.TryParse(user?.FindFirst(claimType)?.Value, out var value) ? value : null;

    /// <summary>Falls back to the entity's own TenantId property (every audited type has one) when there's no authenticated caller to read the claim from — e.g. RegisterTenant's own Tenant row.</summary>
    private static Guid? TryGetTenantIdProperty(EntityEntry entry) =>
        entry.Properties.SingleOrDefault(p => p.Metadata.Name == "TenantId")?.CurrentValue as Guid?;

    private static string GetPrimaryKeyValue(EntityEntry entry) =>
        string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString() ?? ""));

    private static string Serialize(EntityEntry entry, bool useOriginalValues)
    {
        var values = entry.Properties.ToDictionary(
            p => p.Metadata.Name,
            p => useOriginalValues ? p.OriginalValue : p.CurrentValue);

        return JsonSerializer.Serialize(values);
    }
}
