using System.ComponentModel.DataAnnotations;
using Szamla.Domain.Partners;

namespace Szamla.Api.Contracts.Partners;

public sealed record CreatePartnerRequest(
    [Required, MaxLength(200)] string Name,
    bool IsPrivatePerson,
    PartnerCountryCategory CountryCategory,
    [Required, MaxLength(2)] string CountryCode,
    [Required, MaxLength(500)] string Address,
    string? TaxId = null,
    string? EuVatId = null,
    [EmailAddress] string? Email = null,
    int? PaymentTermDays = null);

public sealed record UpdatePartnerRequest(
    [Required, MaxLength(500)] string Address,
    [EmailAddress] string? Email,
    int? PaymentTermDays);
