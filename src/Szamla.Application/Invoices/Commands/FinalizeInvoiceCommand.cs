using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Invoices.Commands;

/// <summary>Finalizes a draft invoice, assigning it the next number from the given series. Returns the assigned number.</summary>
public sealed record FinalizeInvoiceCommand(Guid TenantId, Guid InvoiceId, Guid InvoiceSeriesId) : ICommand<string>;
