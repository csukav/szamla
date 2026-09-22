namespace Szamla.Infrastructure.Auditing;

/// <summary>
/// One row per Added/Modified/Deleted change to an audited entity (see
/// AuditSaveChangesInterceptor for which entity types qualify and exactly what gets captured).
/// Append-only by convention — nothing in this codebase ever updates or deletes an AuditLog row.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null only for actions that precede a tenant existing (e.g. the RegisterTenant command's own Tenant row).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Null for actions with no authenticated user (background jobs, the pre-login part of registration).</summary>
    public Guid? UserId { get; set; }

    public string Action { get; set; } = default!;

    public string EntityType { get; set; } = default!;

    public string EntityId { get; set; } = default!;

    /// <summary>JSON snapshot of scalar property values before the change. Null for Added.</summary>
    public string? BeforeState { get; set; }

    /// <summary>JSON snapshot of scalar property values after the change. Null for Deleted.</summary>
    public string? AfterState { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? IpAddress { get; set; }
}
