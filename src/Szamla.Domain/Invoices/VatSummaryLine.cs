using Szamla.Domain.Common;

namespace Szamla.Domain.Invoices;

/// <summary>One row of the invoice's mandatory VAT-rate breakdown table (áfa-kulcsonkénti bontás): all lines sharing a VatRate, summed.</summary>
public sealed record VatSummaryLine(VatRate VatRate, Money NetAmount, Money VatAmount, Money GrossAmount);
