using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Application.Invoices.Commands;

public sealed class FinalizeInvoiceCommandHandler(IApplicationDbContext dbContext, IInvoiceNumberGenerator numberGenerator)
    : ICommandHandler<FinalizeInvoiceCommand, string>
{
    public async Task<string> Handle(FinalizeInvoiceCommand command, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == command.InvoiceId && i.TenantId == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("A számla nem található.");

        var series = await dbContext.InvoiceSeries
            .SingleOrDefaultAsync(s => s.Id == command.InvoiceSeriesId && s.TenantId == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("A számlatömb nem található.");

        if (!series.IsActive)
        {
            throw new ConflictException("A kiválasztott számlatömb inaktív.");
        }

        var year = invoice.IssueDate.Year;
        var sequence = await numberGenerator.NextAsync(command.TenantId, series.Id, year, cancellationToken);
        var number = series.FormatNumber(year, sequence);

        invoice.Finalize(number, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return number;
    }
}
