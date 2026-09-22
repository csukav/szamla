namespace Szamla.Application.Common.Interfaces;

/// <summary>
/// Atomically allocates the next sequence number in a tenant's invoice series for a given year.
/// Backed by a single-statement PostgreSQL UPSERT (see InvoiceNumberGenerator in Infrastructure),
/// so concurrent callers never collide or skip a number — proven by
/// InvoiceNumberGeneratorConcurrencyTests. A draft invoice must never call this: only finalization
/// consumes a number.
/// </summary>
public interface IInvoiceNumberGenerator
{
    Task<long> NextAsync(Guid tenantId, Guid seriesId, int year, CancellationToken cancellationToken = default);
}
