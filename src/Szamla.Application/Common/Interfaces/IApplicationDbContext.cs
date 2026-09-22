using Microsoft.EntityFrameworkCore;
using Szamla.Domain.Invoices;
using Szamla.Domain.Partners;
using Szamla.Domain.Tenants;

namespace Szamla.Application.Common.Interfaces;

/// <summary>
/// The persistence surface the Application layer is allowed to see. Implemented by
/// Szamla.Infrastructure's ApplicationDbContext; keeps Application free of EF Core's
/// provider-specific and Identity-specific concerns.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<Partner> Partners { get; }

    DbSet<Invoice> Invoices { get; }

    /// <summary>
    /// Exposed alongside Invoices (not reached only through Invoice.Lines) because EF Core can't
    /// reliably infer Added vs Modified for a client-Guid-keyed child reached only via navigation
    /// fixup on an already-tracked parent — a newly created line looks identical to "existing row,
    /// just edited" once its key is set. Handlers that replace an invoice's lines add/remove
    /// through this DbSet explicitly instead of relying on that inference. See
    /// ReplaceDraftInvoiceLinesCommandHandler.
    /// </summary>
    DbSet<InvoiceLine> InvoiceLines { get; }

    DbSet<InvoiceSeries> InvoiceSeries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
