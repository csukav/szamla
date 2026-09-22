using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Application.Partners.Commands;

public sealed class UpdatePartnerCommandHandler(IApplicationDbContext dbContext) : ICommandHandler<UpdatePartnerCommand, Guid>
{
    public async Task<Guid> Handle(UpdatePartnerCommand command, CancellationToken cancellationToken)
    {
        var partner = await dbContext.Partners
            .SingleOrDefaultAsync(p => p.Id == command.PartnerId && p.TenantId == command.TenantId, cancellationToken)
            ?? throw new NotFoundException("A partner nem található.");

        partner.UpdateContactDetails(command.Address, command.Email, command.PaymentTermDays);
        await dbContext.SaveChangesAsync(cancellationToken);

        return partner.Id;
    }
}
