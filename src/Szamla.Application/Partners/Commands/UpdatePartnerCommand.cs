using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Partners.Commands;

public sealed record UpdatePartnerCommand(
    Guid TenantId,
    Guid PartnerId,
    string Address,
    string? Email,
    int? PaymentTermDays) : ICommand<Guid>;
