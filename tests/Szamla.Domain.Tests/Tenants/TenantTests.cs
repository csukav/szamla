using FluentAssertions;
using Szamla.Domain.Tenants;
using Xunit;

namespace Szamla.Domain.Tests.Tenants;

public class TenantTests
{
    [Fact]
    public void Create_WithValidData_SetsPropertiesAndDefaults()
    {
        var tenant = Tenant.Create("Teszt Kft.", "12345678-1-42", "1011 Budapest, Fő utca 1.");

        tenant.Id.Should().NotBeEmpty();
        tenant.Name.Should().Be("Teszt Kft.");
        tenant.TaxId.Should().Be("12345678-1-42");
        tenant.DefaultCurrency.Should().Be("HUF");
        tenant.IsActive.Should().BeTrue();
        tenant.IsVatExempt.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "12345678-1-42", "cím")]
    [InlineData("Név", "", "cím")]
    [InlineData("Név", "12345678-1-42", "")]
    public void Create_WithMissingRequiredField_Throws(string name, string taxId, string address)
    {
        var act = () => Tenant.Create(name, taxId, address);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithInvalidCurrencyCode_Throws()
    {
        var act = () => Tenant.Create("Teszt Kft.", "12345678-1-42", "cím", "HU");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProfile_ChangesEditableFieldsAndSetsModifiedTimestamp()
    {
        var tenant = Tenant.Create("Régi Név", "12345678-1-42", "Régi cím");

        tenant.UpdateProfile("Új Név", "Új cím", "HU00-1111-2222-3333", "https://example.com/logo.png");

        tenant.Name.Should().Be("Új Név");
        tenant.Address.Should().Be("Új cím");
        tenant.BankAccount.Should().Be("HU00-1111-2222-3333");
        tenant.LogoUrl.Should().Be("https://example.com/logo.png");
        tenant.ModifiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var tenant = Tenant.Create("Teszt Kft.", "12345678-1-42", "cím");

        tenant.Deactivate();

        tenant.IsActive.Should().BeFalse();
    }
}
