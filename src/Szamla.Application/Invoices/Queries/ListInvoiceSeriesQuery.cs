using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Invoices.Queries;

public sealed record ListInvoiceSeriesQuery(Guid TenantId) : IQuery<IReadOnlyList<InvoiceSeriesDto>>;

public sealed record InvoiceSeriesDto(Guid Id, string Prefix, bool IsActive);
