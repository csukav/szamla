using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Application.Invoices.Queries;

public sealed class ListInvoicesQueryHandler(IApplicationDbContext dbContext) : IQueryHandler<ListInvoicesQuery, IReadOnlyList<InvoiceSummaryDto>>
{
    public async Task<IReadOnlyList<InvoiceSummaryDto>> Handle(ListInvoicesQuery query, CancellationToken cancellationToken)
    {
        var invoices = dbContext.Invoices.Where(i => i.TenantId == query.TenantId);

        if (query.Status is not null)
        {
            invoices = invoices.Where(i => i.Status == query.Status);
        }

        var results = await invoices.OrderByDescending(i => i.IssueDate).ToListAsync(cancellationToken);
        return results.Select(InvoiceSummaryDto.FromDomain).ToList();
    }
}
