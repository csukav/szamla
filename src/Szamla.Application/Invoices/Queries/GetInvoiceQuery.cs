using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Invoices.Queries;

public sealed record GetInvoiceQuery(Guid TenantId, Guid InvoiceId) : IQuery<InvoiceDto?>;
