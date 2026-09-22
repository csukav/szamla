using Szamla.Domain.Common;

namespace Szamla.Domain.Partners;

/// <summary>A customer (vevő) invoices are issued to. Tenant-scoped: each tenant maintains its own partner list.</summary>
public sealed class Partner : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = default!;

    public bool IsPrivatePerson { get; private set; }

    public PartnerCountryCategory CountryCategory { get; private set; }

    public string CountryCode { get; private set; } = default!;

    /// <summary>Magyar adószám (belföldi cégnél kötelező).</summary>
    public string? TaxId { get; private set; }

    /// <summary>EU-s közösségi adószám (pl. HU12345678).</summary>
    public string? EuVatId { get; private set; }

    public string Address { get; private set; } = default!;

    public string? Email { get; private set; }

    public int? PaymentTermDays { get; private set; }

    private Partner()
    {
    }

    public static Partner Create(
        Guid tenantId,
        string name,
        bool isPrivatePerson,
        PartnerCountryCategory countryCategory,
        string countryCode,
        string address,
        string? taxId = null,
        string? euVatId = null,
        string? email = null,
        int? paymentTermDays = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A partner neve kötelező.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A cím kötelező.", nameof(address));
        }

        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            throw new ArgumentException("Az országkódnak ISO 3166-1 alpha-2 formátumúnak kell lennie.", nameof(countryCode));
        }

        // NAV/Áfa tv. szerint a belföldi, nem magánszemély vevő adószáma minden esetben feltüntetendő.
        // (Vannak további, összeghatárhoz kötött szabályok az adószám kötelező feltüntetésére —
        // ezeket a számla-életciklus fázisban, jogi ellenőrzés után kell pontosítani.)
        if (!isPrivatePerson && countryCategory == PartnerCountryCategory.Domestic && string.IsNullOrWhiteSpace(taxId))
        {
            throw new ArgumentException("Belföldi cég vevőnél az adószám kötelező.", nameof(taxId));
        }

        if (paymentTermDays is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermDays), paymentTermDays, "A fizetési határidő napjainak száma nem lehet negatív.");
        }

        return new Partner
        {
            TenantId = tenantId,
            Name = name.Trim(),
            IsPrivatePerson = isPrivatePerson,
            CountryCategory = countryCategory,
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            TaxId = string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim(),
            EuVatId = string.IsNullOrWhiteSpace(euVatId) ? null : euVatId.Trim(),
            Address = address.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            PaymentTermDays = paymentTermDays,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateContactDetails(string address, string? email, int? paymentTermDays)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A cím kötelező.", nameof(address));
        }

        if (paymentTermDays is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermDays), paymentTermDays, "A fizetési határidő napjainak száma nem lehet negatív.");
        }

        Address = address.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        PaymentTermDays = paymentTermDays;
        ModifiedAtUtc = DateTimeOffset.UtcNow;
    }
}
