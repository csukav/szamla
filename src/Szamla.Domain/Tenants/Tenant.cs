using Szamla.Domain.Common;

namespace Szamla.Domain.Tenants;

/// <summary>
/// A single paying customer of the SaaS (a business that issues invoices through the system).
/// The tenant itself is the root of the multi-tenancy boundary, so it is not tenant-scoped.
/// </summary>
public class Tenant : AuditableEntity
{
    public string Name { get; private set; } = default!;

    public string TaxId { get; private set; } = default!;

    public string Address { get; private set; } = default!;

    public string? BankAccount { get; private set; }

    public string? LogoUrl { get; private set; }

    public bool IsVatExempt { get; private set; }

    public string DefaultCurrency { get; private set; } = "HUF";

    public bool IsActive { get; private set; } = true;

    private Tenant()
    {
    }

    public static Tenant Create(string name, string taxId, string address, string defaultCurrency = "HUF")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A cégnév kötelező.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(taxId))
        {
            throw new ArgumentException("Az adószám kötelező.", nameof(taxId));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A cím kötelező.", nameof(address));
        }

        if (string.IsNullOrWhiteSpace(defaultCurrency) || defaultCurrency.Length != 3)
        {
            throw new ArgumentException("A pénznemnek ISO 4217 hárombetűs kódnak kell lennie.", nameof(defaultCurrency));
        }

        return new Tenant
        {
            Name = name.Trim(),
            TaxId = taxId.Trim(),
            Address = address.Trim(),
            DefaultCurrency = defaultCurrency.Trim().ToUpperInvariant(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateProfile(string name, string address, string? bankAccount, string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A cégnév kötelező.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A cím kötelező.", nameof(address));
        }

        Name = name.Trim();
        Address = address.Trim();
        BankAccount = bankAccount;
        LogoUrl = logoUrl;
        ModifiedAtUtc = DateTimeOffset.UtcNow;
    }

    public void SetVatExempt(bool isVatExempt)
    {
        IsVatExempt = isVatExempt;
        ModifiedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        ModifiedAtUtc = DateTimeOffset.UtcNow;
    }
}
