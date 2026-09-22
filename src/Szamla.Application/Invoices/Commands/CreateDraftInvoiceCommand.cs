using Szamla.Application.Common.Cqrs;
using Szamla.Application.Invoices.Common;
using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Commands;

public sealed record CreateDraftInvoiceCommand(
    Guid TenantId,
    Guid PartnerId,
    DateOnly IssueDate,
    DateOnly PerformanceDate,
    DateOnly PaymentDueDate,
    PaymentMethod PaymentMethod,
    string Currency,
    IReadOnlyList<InvoiceLineInput> Lines,
    decimal? ExchangeRate = null,
    string? ExchangeRateSource = null,
    DateOnly? ExchangeRateDate = null) : ICommand<Guid>;
