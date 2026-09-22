using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Common;

public sealed record VatRateInput(VatRateKind Kind, decimal? Percentage, VatExemptionReason? ExemptionReason)
{
    public VatRate ToDomain() => Kind switch
    {
        VatRateKind.Percentage => VatRate.OfPercentage(Percentage
            ?? throw new ArgumentException("Százalékos ÁFA-kulcsnál a Percentage kötelező.", nameof(Percentage))),
        VatRateKind.Exempt => VatRate.Exempt(ExemptionReason
            ?? throw new ArgumentException("Mentesség esetén az ExemptionReason kötelező.", nameof(ExemptionReason))),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null),
    };
}

public sealed record InvoiceLineInput(
    string Description,
    decimal Quantity,
    string Unit,
    decimal NetUnitPriceAmount,
    VatRateInput VatRate);
