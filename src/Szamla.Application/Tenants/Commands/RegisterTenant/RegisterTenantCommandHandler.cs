using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Common.Interfaces;
using Szamla.Domain.Tenants;

namespace Szamla.Application.Tenants.Commands.RegisterTenant;

public sealed class RegisterTenantCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<RegisterTenantCommand, Guid>
{
    public async Task<Guid> Handle(RegisterTenantCommand command, CancellationToken cancellationToken)
    {
        var taxIdAlreadyRegistered = await dbContext.Tenants
            .AnyAsync(t => t.TaxId == command.TaxId, cancellationToken);

        if (taxIdAlreadyRegistered)
        {
            throw new ConflictException("Ezzel az adószámmal már regisztráltak bérlőt.");
        }

        var tenant = Tenant.Create(command.CompanyName, command.TaxId, command.Address, command.DefaultCurrency);

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }
}
