using Microsoft.EntityFrameworkCore;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Interfaces;

namespace Szamla.Application.Partners.Queries;

public sealed class ListPartnersQueryHandler(IApplicationDbContext dbContext) : IQueryHandler<ListPartnersQuery, IReadOnlyList<PartnerDto>>
{
    public async Task<IReadOnlyList<PartnerDto>> Handle(ListPartnersQuery query, CancellationToken cancellationToken)
    {
        var partners = dbContext.Partners.Where(p => p.TenantId == query.TenantId);

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            // Deliberately not EF.Functions.ILike: that's Npgsql-specific, and Application must
            // stay provider-agnostic. ToLower/Contains translates to a portable case-insensitive
            // LIKE on every relational provider EF Core supports.
            var needle = query.SearchText.Trim().ToLowerInvariant();
            partners = partners.Where(p => p.Name.ToLower().Contains(needle));
        }

        var results = await partners.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return results.Select(PartnerDto.FromDomain).ToList();
    }
}
