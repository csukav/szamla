using FluentAssertions;
using Szamla.Domain.Common;
using Szamla.Domain.Invoices;
using Xunit;

namespace Szamla.Domain.Tests.Invoices;

public class InvoiceLineTests
{
    [Fact]
    public void Create_WithStandardRate_ComputesNetVatAndGrossCorrectly()
    {
        var line = InvoiceLine.Create("Tanácsadás", 2m, "óra", new Money(10000m, "HUF"), VatRate.OfPercentage(0.27m));

        line.NetAmount.Should().Be(new Money(20000m, "HUF"));
        line.VatAmount.Should().Be(new Money(5400m, "HUF"));
        line.GrossAmount.Should().Be(new Money(25400m, "HUF"));
    }

    [Fact]
    public void Create_WithExemptRate_HasZeroVatAmount()
    {
        var line = InvoiceLine.Create("Oktatás", 1m, "db", new Money(50000m, "HUF"), VatRate.Exempt(VatExemptionReason.SubjectExempt));

        line.VatAmount.Should().Be(new Money(0m, "HUF"));
        line.GrossAmount.Should().Be(line.NetAmount);
    }

    [Fact]
    public void Create_RoundsNetAndVatAmountsToTwoDecimals()
    {
        var line = InvoiceLine.Create("Törtmennyiség", 0.333m, "kg", new Money(3333m, "HUF"), VatRate.OfPercentage(0.27m));

        // 0.333 * 3333 = 1109.889 -> rounds to 1109.89
        line.NetAmount.Amount.Should().Be(1109.89m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveQuantity_Throws(decimal quantity)
    {
        var act = () => InvoiceLine.Create("Tétel", quantity, "db", new Money(100m, "HUF"), VatRate.OfPercentage(0.27m));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithEmptyDescription_Throws()
    {
        var act = () => InvoiceLine.Create("", 1m, "db", new Money(100m, "HUF"), VatRate.OfPercentage(0.27m));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateStornoMirror_NegatesQuantityAndAllComputedAmounts()
    {
        var original = InvoiceLine.Create("Tanácsadás", 2m, "óra", new Money(10000m, "HUF"), VatRate.OfPercentage(0.27m));

        var mirror = InvoiceLine.CreateStornoMirror(original);

        mirror.Quantity.Should().Be(-2m);
        mirror.NetAmount.Should().Be(new Money(-20000m, "HUF"));
        mirror.VatAmount.Should().Be(new Money(-5400m, "HUF"));
        mirror.GrossAmount.Should().Be(new Money(-25400m, "HUF"));
        (original.NetAmount + mirror.NetAmount).Should().Be(Money.Zero("HUF"));
    }
}
