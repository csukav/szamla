using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Partners;

namespace Szamla.Application.Partners.Commands;

public sealed class CreatePartnerCommandHandler(IApplicationDbContext dbContext) : ICommandHandler<CreatePartnerCommand, Guid>
{
    public async Task<Guid> Handle(CreatePartnerCommand command, CancellationToken cancellationToken)
    {
        var partner = Partner.Create(
            command.TenantId, command.Name, command.IsPrivatePerson, command.CountryCategory, command.CountryCode,
            command.Address, command.TaxId, command.EuVatId, command.Email, command.PaymentTermDays);

        dbContext.Partners.Add(partner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return partner.Id;
    }
}
