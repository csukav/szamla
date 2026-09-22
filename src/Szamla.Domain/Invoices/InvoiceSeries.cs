using Szamla.Domain.Common;

namespace Szamla.Domain.Invoices;

/// <summary>
/// A számlatömb: bérlőnként a folyamatos, hézagmentes sorszámozás egysége (előtag; az évenként
/// nullázódó számláló maga az Infrastructure rétegben, a konkurenciabiztos InvoiceNumberCounter
/// táblában él — lásd Szamla.Infrastructure.Invoicing.InvoiceNumberGenerator).
/// </summary>
public sealed class InvoiceSeries : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; private set; }

    public string Prefix { get; private set; } = default!;

    public bool IsActive { get; private set; } = true;

    private InvoiceSeries()
    {
    }

    public static InvoiceSeries Create(Guid tenantId, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 10)
        {
            throw new ArgumentException("Az előtag 1-10 karakter hosszú lehet.", nameof(prefix));
        }

        return new InvoiceSeries
        {
            TenantId = tenantId,
            Prefix = prefix.Trim().ToUpperInvariant(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        ModifiedAtUtc = DateTimeOffset.UtcNow;
    }

    public string FormatNumber(int year, long sequence) => $"{Prefix}-{year}-{sequence:D6}";
}
