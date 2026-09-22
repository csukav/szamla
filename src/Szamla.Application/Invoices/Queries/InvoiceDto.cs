using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Queries;

public sealed record InvoiceLineDto(
    Guid Id,
    string Description,
    decimal Quantity,
    string Unit,
    decimal NetUnitPriceAmount,
    string VatRateDisplayCode,
    decimal NetAmount,
    decimal VatAmount,
    decimal GrossAmount)
{
    public static InvoiceLineDto FromDomain(InvoiceLine line) => new(
        line.Id, line.Description, line.Quantity, line.Unit, line.NetUnitPrice.Amount,
        line.VatRate.DisplayCode, line.NetAmount.Amount, line.VatAmount.Amount, line.GrossAmount.Amount);
}

public sealed record VatSummaryRowDto(string VatRateDisplayCode, decimal NetAmount, decimal VatAmount, decimal GrossAmount)
{
    public static VatSummaryRowDto FromDomain(VatSummaryLine line) => new(
        line.VatRate.DisplayCode, line.NetAmount.Amount, line.VatAmount.Amount, line.GrossAmount.Amount);
}

public sealed record InvoiceDto(
    Guid Id,
    InvoiceType Type,
    InvoiceStatus Status,
    string? Number,
    Guid? OriginalInvoiceId,
    DateOnly IssueDate,
    DateOnly PerformanceDate,
    DateOnly PaymentDueDate,
    PaymentMethod PaymentMethod,
    string Currency,
    decimal? ExchangeRate,
    string? ExchangeRateSource,
    DateOnly? ExchangeRateDate,
    IssuerSnapshot Issuer,
    PartnerSnapshot Partner,
    IReadOnlyList<InvoiceLineDto> Lines,
    decimal NetTotal,
    decimal VatTotal,
    decimal GrossTotal,
    decimal? VatTotalHufAmount,
    IReadOnlyList<VatSummaryRowDto> VatSummary)
{
    public static InvoiceDto FromDomain(Invoice invoice) => new(
        invoice.Id, invoice.Type, invoice.Status, invoice.Number, invoice.OriginalInvoiceId,
        invoice.IssueDate, invoice.PerformanceDate, invoice.PaymentDueDate, invoice.PaymentMethod,
        invoice.Currency, invoice.ExchangeRate, invoice.ExchangeRateSource, invoice.ExchangeRateDate,
        invoice.Issuer, invoice.Partner,
        invoice.Lines.Select(InvoiceLineDto.FromDomain).ToList(),
        invoice.NetTotal.Amount, invoice.VatTotal.Amount, invoice.GrossTotal.Amount, invoice.VatTotalHufAmount,
        invoice.VatSummary.Select(VatSummaryRowDto.FromDomain).ToList());
}

public sealed record InvoiceSummaryDto(
    Guid Id,
    InvoiceType Type,
    InvoiceStatus Status,
    string? Number,
    DateOnly IssueDate,
    string PartnerName,
    decimal GrossTotal,
    string Currency)
{
    public static InvoiceSummaryDto FromDomain(Invoice invoice) => new(
        invoice.Id, invoice.Type, invoice.Status, invoice.Number, invoice.IssueDate,
        invoice.Partner.Name, invoice.GrossTotal.Amount, invoice.Currency);
}
