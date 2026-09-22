using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Commands;

public sealed class CreateStornoInvoiceCommandHandler(IApplicationDbContext dbContext) : ICommandHandler<CreateStornoInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(CreateStornoInvoiceCommand command, CancellationToken cancellationToken)
    {
        var original = await dbContext.Invoices
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == command.OriginalInvoiceId && i.TenantId == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("Az eredeti számla nem található.");

        var storno = Invoice.CreateStorno(original, command.IssueDate);

        dbContext.Invoices.Add(storno);
        await dbContext.SaveChangesAsync(cancellationToken);

        return storno.Id;
    }
}
