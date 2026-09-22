using Szamla.Domain.Common;

namespace Szamla.Domain.Invoices;

/// <summary>One priced line of an invoice. Net/VAT/gross amounts are computed once at creation and never recomputed in place — editing a line means creating a replacement.</summary>
public sealed class InvoiceLine
{
    public Guid Id { get; private set; }

    public string Description { get; private set; } = default!;

    public decimal Quantity { get; private set; }

    public string Unit { get; private set; } = default!;

    public Money NetUnitPrice { get; private set; }

    public VatRate VatRate { get; private set; } = default!;

    public Money NetAmount { get; private set; }

    public Money VatAmount { get; private set; }

    public Money GrossAmount { get; private set; }

    private InvoiceLine()
    {
    }

    public static InvoiceLine Create(string description, decimal quantity, string unit, Money netUnitPrice, VatRate vatRate)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A mennyiségnek pozitívnak kell lennie.");
        }

        return Build(description, quantity, unit, netUnitPrice, vatRate);
    }

    /// <summary>
    /// Builds the mirror-image line for a storno invoice: same description/unit/price/rate, but
    /// negated quantity (and therefore negated net/VAT/gross amounts) — the standard Hungarian
    /// accounting convention for fully cancelling out an invoice line.
    /// </summary>
    public static InvoiceLine CreateStornoMirror(InvoiceLine original) =>
        Build(original.Description, -original.Quantity, original.Unit, original.NetUnitPrice, original.VatRate);

    private static InvoiceLine Build(string description, decimal quantity, string unit, Money netUnitPrice, VatRate vatRate)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A tétel megnevezése kötelező.", nameof(description));
        }

        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A mennyiség nem lehet nulla.");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("A mennyiségi egység kötelező.", nameof(unit));
        }

        var netAmount = RoundingPolicy.RoundLineAmount(quantity * netUnitPrice.Amount);
        var vatAmount = vatRate.IsZeroLiability
            ? 0m
            : RoundingPolicy.RoundLineAmount(netAmount * vatRate.Percentage!.Value);
        var grossAmount = netAmount + vatAmount;

        return new InvoiceLine
        {
            Id = Guid.NewGuid(),
            Description = description.Trim(),
            Quantity = quantity,
            Unit = unit.Trim(),
            NetUnitPrice = netUnitPrice,
            VatRate = vatRate,
            NetAmount = new Money(netAmount, netUnitPrice.CurrencyCode),
            VatAmount = new Money(vatAmount, netUnitPrice.CurrencyCode),
            GrossAmount = new Money(grossAmount, netUnitPrice.CurrencyCode),
        };
    }
}
