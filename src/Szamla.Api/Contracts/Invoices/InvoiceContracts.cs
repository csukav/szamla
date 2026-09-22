using System.ComponentModel.DataAnnotations;
using Szamla.Application.Invoices.Common;
using Szamla.Domain.Invoices;

namespace Szamla.Api.Contracts.Invoices;

public sealed record InvoiceLineRequest(
    [Required, MaxLength(500)] string Description,
    decimal Quantity,
    [Required, MaxLength(50)] string Unit,
    decimal NetUnitPriceAmount,
    VatRateKind VatRateKind,
    decimal? VatPercentage,
    VatExemptionReason? VatExemptionReason)
{
    public InvoiceLineInput ToApplicationInput() =>
        new(Description, Quantity, Unit, NetUnitPriceAmount, new VatRateInput(VatRateKind, VatPercentage, VatExemptionReason));
}

public sealed record CreateDraftInvoiceRequest(
    Guid PartnerId,
    DateOnly IssueDate,
    DateOnly PerformanceDate,
    DateOnly PaymentDueDate,
    PaymentMethod PaymentMethod,
    [Required, MaxLength(3)] string Currency,
    [Required, MinLength(1)] IReadOnlyList<InvoiceLineRequest> Lines,
    decimal? ExchangeRate = null,
    string? ExchangeRateSource = null,
    DateOnly? ExchangeRateDate = null);

public sealed record ReplaceInvoiceLinesRequest([Required, MinLength(1)] IReadOnlyList<InvoiceLineRequest> Lines);

public sealed record FinalizeInvoiceRequest(Guid InvoiceSeriesId);

public sealed record CreateStornoInvoiceRequest(DateOnly IssueDate);

public sealed record CreateModificationInvoiceRequest(
    DateOnly IssueDate,
    DateOnly PerformanceDate,
    DateOnly PaymentDueDate,
    PaymentMethod PaymentMethod,
    [Required, MinLength(1)] IReadOnlyList<InvoiceLineRequest> CorrectionLines);

public sealed record CreateInvoiceSeriesRequest([Required, MaxLength(10)] string Prefix);
