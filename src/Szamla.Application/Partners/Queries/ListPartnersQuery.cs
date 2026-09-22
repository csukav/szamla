using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Partners.Queries;

public sealed record ListPartnersQuery(Guid TenantId, string? SearchText = null) : IQuery<IReadOnlyList<PartnerDto>>;
