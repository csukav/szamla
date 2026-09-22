using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Commands;

public sealed class CreateInvoiceSeriesCommandHandler(IApplicationDbContext dbContext) : ICommandHandler<CreateInvoiceSeriesCommand, Guid>
{
    public async Task<Guid> Handle(CreateInvoiceSeriesCommand command, CancellationToken cancellationToken)
    {
        var normalizedPrefix = command.Prefix.Trim().ToUpperInvariant();
        var alreadyExists = await dbContext.InvoiceSeries
            .AnyAsync(s => s.TenantId == command.TenantId && s.Prefix == normalizedPrefix, cancellationToken);
        if (alreadyExists)
        {
            throw new ConflictException("Ezzel az előtaggal már létezik számlatömb.");
        }

        var series = InvoiceSeries.Create(command.TenantId, command.Prefix);
        dbContext.InvoiceSeries.Add(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        return series.Id;
    }
}
