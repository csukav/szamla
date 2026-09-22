using Szamla.Application.Common.Cqrs;
using Szamla.Domain.Partners;

namespace Szamla.Application.Partners.Commands;

public sealed record CreatePartnerCommand(
    Guid TenantId,
    string Name,
    bool IsPrivatePerson,
    PartnerCountryCategory CountryCategory,
    string CountryCode,
    string Address,
    string? TaxId = null,
    string? EuVatId = null,
    string? Email = null,
    int? PaymentTermDays = null) : ICommand<Guid>;
