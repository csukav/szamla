namespace Szamla.Infrastructure.Invoicing;

/// <summary>
/// EF entity for the (TenantId, SeriesId, Year) counter row. LastNumber is only ever mutated
/// through the atomic UPSERT in InvoiceNumberGenerator — never loaded and saved via normal EF
/// change tracking, which would reintroduce the race condition this table exists to avoid.
/// </summary>
public class InvoiceNumberCounter
{
    public Guid TenantId { get; set; }

    public Guid SeriesId { get; set; }

    public int Year { get; set; }

    public long LastNumber { get; set; }
}
