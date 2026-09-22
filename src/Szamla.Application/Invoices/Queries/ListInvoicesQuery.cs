using Szamla.Application.Common.Cqrs;
using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Queries;

public sealed record ListInvoicesQuery(Guid TenantId, InvoiceStatus? Status = null) : IQuery<IReadOnlyList<InvoiceSummaryDto>>;
