using FluentAssertions;
using Szamla.Domain.Invoices;
using Xunit;

namespace Szamla.Domain.Tests.Invoices;

public class VatRateTests
{
    [Theory]
    [InlineData(0.27)]
    [InlineData(0.18)]
    [InlineData(0.05)]
    [InlineData(0.00)]
    public void OfPercentage_WithSupportedRate_Succeeds(decimal rate)
    {
        var vatRate = VatRate.OfPercentage(rate);

        vatRate.Kind.Should().Be(VatRateKind.Percentage);
        vatRate.Percentage.Should().Be(rate);
    }

    [Fact]
    public void OfPercentage_WithUnsupportedRate_Throws()
    {
        var act = () => VatRate.OfPercentage(0.15m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OfPercentage_AtZero_IsZeroLiability()
    {
        VatRate.OfPercentage(0.00m).IsZeroLiability.Should().BeTrue();
    }

    [Fact]
    public void OfPercentage_AboveZero_IsNotZeroLiability()
    {
        VatRate.OfPercentage(0.27m).IsZeroLiability.Should().BeFalse();
    }

    [Fact]
    public void Exempt_IsAlwaysZeroLiability()
    {
        VatRate.Exempt(VatExemptionReason.SubjectExempt).IsZeroLiability.Should().BeTrue();
    }

    [Theory]
    [InlineData(VatExemptionReason.SubjectExempt, "AAM")]
    [InlineData(VatExemptionReason.ObjectExempt, "TAM")]
    [InlineData(VatExemptionReason.IntraCommunitySupply, "EUE")]
    [InlineData(VatExemptionReason.IntraCommunityReverseCharge, "EUFAD37")]
    public void Exempt_DisplayCode_MatchesKnownAbbreviation(VatExemptionReason reason, string expectedCode)
    {
        VatRate.Exempt(reason).DisplayCode.Should().Be(expectedCode);
    }

    [Fact]
    public void Equals_TwoInstancesWithSamePercentage_AreEqual()
    {
        VatRate.OfPercentage(0.27m).Should().Be(VatRate.OfPercentage(0.27m));
    }

    [Fact]
    public void Equals_PercentageAndExempt_AreNotEqual()
    {
        VatRate.OfPercentage(0.00m).Should().NotBe(VatRate.Exempt(VatExemptionReason.SubjectExempt));
    }

    [Fact]
    public void Equals_DifferentExemptionReasons_AreNotEqual()
    {
        VatRate.Exempt(VatExemptionReason.SubjectExempt).Should().NotBe(VatRate.Exempt(VatExemptionReason.ObjectExempt));
    }
}
