using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Common;
using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Commands;

public sealed class ReplaceDraftInvoiceLinesCommandHandler(IApplicationDbContext dbContext) : ICommandHandler<ReplaceDraftInvoiceLinesCommand, Guid>
{
    public async Task<Guid> Handle(ReplaceDraftInvoiceLinesCommand command, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == command.InvoiceId && i.TenantId == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("A számla nem található.");

        var oldLines = invoice.Lines.ToList();
        var newLines = command.Lines
            .Select(l => InvoiceLine.Create(l.Description, l.Quantity, l.Unit, new Money(l.NetUnitPriceAmount, invoice.Currency), l.VatRate.ToDomain()))
            .ToList();

        invoice.ReplaceLines(newLines);

        // Explicit, not left to EF's graph-fixup inference: InvoiceLine's key is a client-assigned
        // Guid, so a "new" line reached only via Invoice.Lines on an already-tracked Invoice looks
        // exactly like "existing row, edited" to EF Core — it would generate an UPDATE against a
        // row that was never inserted. See the comment on IApplicationDbContext.InvoiceLines.
        dbContext.InvoiceLines.RemoveRange(oldLines);
        dbContext.InvoiceLines.AddRange(newLines);

        await dbContext.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
