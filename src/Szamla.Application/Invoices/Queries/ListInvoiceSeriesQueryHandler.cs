using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Application.Invoices.Queries;

public sealed class ListInvoiceSeriesQueryHandler(IApplicationDbContext dbContext) : IQueryHandler<ListInvoiceSeriesQuery, IReadOnlyList<InvoiceSeriesDto>>
{
    public async Task<IReadOnlyList<InvoiceSeriesDto>> Handle(ListInvoiceSeriesQuery query, CancellationToken cancellationToken)
        => await dbContext.InvoiceSeries
            .Where(s => s.TenantId == query.TenantId)
            .OrderBy(s => s.Prefix)
            .Select(s => new InvoiceSeriesDto(s.Id, s.Prefix, s.IsActive))
            .ToListAsync(cancellationToken);
}
