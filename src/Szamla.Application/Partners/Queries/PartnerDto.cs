using Szamla.Domain.Partners;

namespace Szamla.Application.Partners.Queries;

public sealed record PartnerDto(
    Guid Id,
    string Name,
    bool IsPrivatePerson,
    PartnerCountryCategory CountryCategory,
    string CountryCode,
    string Address,
    string? TaxId,
    string? EuVatId,
    string? Email,
    int? PaymentTermDays)
{
    public static PartnerDto FromDomain(Partner partner) => new(
        partner.Id, partner.Name, partner.IsPrivatePerson, partner.CountryCategory, partner.CountryCode,
        partner.Address, partner.TaxId, partner.EuVatId, partner.Email, partner.PaymentTermDays);
}
