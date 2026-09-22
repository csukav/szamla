using Szamla.Domain.Partners;

namespace Szamla.Domain.Invoices;

/// <summary>A copy of the buyer's details as they were at issue time, embedded in the invoice for the same immutability reason as <see cref="IssuerSnapshot"/>.</summary>
public sealed record PartnerSnapshot(
    string Name,
    bool IsPrivatePerson,
    PartnerCountryCategory CountryCategory,
    string CountryCode,
    string Address,
    string? TaxId,
    string? EuVatId,
    string? Email);
