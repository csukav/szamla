using FluentAssertions;
using Szamla.Domain.Partners;
using Xunit;

namespace Szamla.Domain.Tests.Partners;

public class PartnerTests
{
    [Fact]
    public void Create_DomesticCompanyWithTaxId_Succeeds()
    {
        var partner = Partner.Create(
            Guid.NewGuid(), "Vevő Kft.", isPrivatePerson: false,
            PartnerCountryCategory.Domestic, "HU", "1011 Budapest, Fő utca 1.", taxId: "12345678-1-42");

        partner.TaxId.Should().Be("12345678-1-42");
    }

    [Fact]
    public void Create_DomesticCompanyWithoutTaxId_Throws()
    {
        var act = () => Partner.Create(
            Guid.NewGuid(), "Vevő Kft.", isPrivatePerson: false,
            PartnerCountryCategory.Domestic, "HU", "cím", taxId: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_DomesticPrivatePersonWithoutTaxId_Succeeds()
    {
        var partner = Partner.Create(
            Guid.NewGuid(), "Teszt Elek", isPrivatePerson: true,
            PartnerCountryCategory.Domestic, "HU", "cím");

        partner.TaxId.Should().BeNull();
    }

    [Fact]
    public void Create_EuCompanyWithEuVatId_Succeeds()
    {
        var partner = Partner.Create(
            Guid.NewGuid(), "EU Vevő GmbH", isPrivatePerson: false,
            PartnerCountryCategory.EuMemberState, "DE", "cím", euVatId: "DE123456789");

        partner.EuVatId.Should().Be("DE123456789");
    }

    [Theory]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("HUN")]
    public void Create_WithInvalidCountryCode_Throws(string countryCode)
    {
        var act = () => Partner.Create(
            Guid.NewGuid(), "Vevő", isPrivatePerson: true,
            PartnerCountryCategory.Domestic, countryCode, "cím");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNegativePaymentTermDays_Throws()
    {
        var act = () => Partner.Create(
            Guid.NewGuid(), "Vevő", isPrivatePerson: true,
            PartnerCountryCategory.Domestic, "HU", "cím", paymentTermDays: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateContactDetails_ChangesAddressEmailAndPaymentTerm()
    {
        var partner = Partner.Create(
            Guid.NewGuid(), "Vevő Kft.", isPrivatePerson: false,
            PartnerCountryCategory.Domestic, "HU", "régi cím", taxId: "12345678-1-42");

        partner.UpdateContactDetails("új cím", "uj@example.com", 30);

        partner.Address.Should().Be("új cím");
        partner.Email.Should().Be("uj@example.com");
        partner.PaymentTermDays.Should().Be(30);
        partner.ModifiedAtUtc.Should().NotBeNull();
    }
}
