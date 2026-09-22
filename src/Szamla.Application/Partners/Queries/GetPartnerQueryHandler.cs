using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Application.Partners.Queries;

public sealed class GetPartnerQueryHandler(IApplicationDbContext dbContext) : IQueryHandler<GetPartnerQuery, PartnerDto?>
{
    public async Task<PartnerDto?> Handle(GetPartnerQuery query, CancellationToken cancellationToken)
    {
        var partner = await dbContext.Partners
            .SingleOrDefaultAsync(p => p.Id == query.PartnerId && p.TenantId == query.TenantId, cancellationToken);

        return partner is null ? null : PartnerDto.FromDomain(partner);
    }
}
