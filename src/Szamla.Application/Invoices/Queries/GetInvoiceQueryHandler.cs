using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Application.Invoices.Queries;

public sealed class GetInvoiceQueryHandler(IApplicationDbContext dbContext) : IQueryHandler<GetInvoiceQuery, InvoiceDto?>
{
    public async Task<InvoiceDto?> Handle(GetInvoiceQuery query, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == query.InvoiceId && i.TenantId == query.TenantId, cancellationToken);

        return invoice is null ? null : InvoiceDto.FromDomain(invoice);
    }
}
