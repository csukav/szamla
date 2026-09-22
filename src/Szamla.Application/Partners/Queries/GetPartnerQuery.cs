using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Partners.Queries;

public sealed record GetPartnerQuery(Guid TenantId, Guid PartnerId) : IQuery<PartnerDto?>;
