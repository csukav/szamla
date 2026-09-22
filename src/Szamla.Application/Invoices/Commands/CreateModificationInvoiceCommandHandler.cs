using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Common;
using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Commands;

public sealed class CreateModificationInvoiceCommandHandler(IApplicationDbContext dbContext) : ICommandHandler<CreateModificationInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(CreateModificationInvoiceCommand command, CancellationToken cancellationToken)
    {
        var original = await dbContext.Invoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == command.OriginalInvoiceId && i.TenantId == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Az eredeti számla nem található.");

        var lines = command.CorrectionLines
            .Select(l => InvoiceLine.Create(l.Description, l.Quantity, l.Unit, new Money(l.NetUnitPriceAmount, original.Currency), l.VatRate.ToDomain()))
            .ToList();

        var modification = Invoice.CreateModification(
            original, command.IssueDate, command.PerformanceDate, command.PaymentDueDate, command.PaymentMethod, lines);

        dbContext.Invoices.Add(modification);
        await dbContext.SaveChangesAsync(cancellationToken);

        return modification.Id;
    }
}
