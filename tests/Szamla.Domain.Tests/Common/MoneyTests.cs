using FluentAssertions;
using Szamla.Domain.Common;
using Szamla.Domain.Common.Exceptions;
using Xunit;

namespace Szamla.Domain.Tests.Common;

public class MoneyTests
{
    [Fact]
    public void Constructor_NormalizesCurrencyCodeToUppercase()
    {
        var money = new Money(100m, "huf");

        money.CurrencyCode.Should().Be("HUF");
    }

    [Theory]
    [InlineData("")]
    [InlineData("HU")]
    [InlineData("HUFF")]
    public void Constructor_WithInvalidCurrencyCode_Throws(string currencyCode)
    {
        var act = () => new Money(100m, currencyCode);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_WithSameCurrency_SumsAmounts()
    {
        var result = new Money(100m, "HUF") + new Money(50m, "HUF");

        result.Should().Be(new Money(150m, "HUF"));
    }

    [Fact]
    public void Add_WithDifferentCurrencies_ThrowsCurrencyMismatchException()
    {
        var act = () => new Money(100m, "HUF") + new Money(50m, "EUR");

        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Subtract_WithSameCurrency_SubtractsAmounts()
    {
        var result = new Money(100m, "HUF") - new Money(30m, "HUF");

        result.Should().Be(new Money(70m, "HUF"));
    }

    [Fact]
    public void Multiply_ByFactor_ScalesAmount()
    {
        var result = new Money(10m, "HUF") * 3;

        result.Should().Be(new Money(30m, "HUF"));
    }

    [Fact]
    public void ComparisonOperators_WithSameCurrency_CompareAmounts()
    {
        var smaller = new Money(10m, "HUF");
        var equalToSmaller = new Money(10m, "HUF");
        var larger = new Money(20m, "HUF");

        (smaller < larger).Should().BeTrue();
        (larger > smaller).Should().BeTrue();
        (smaller <= equalToSmaller).Should().BeTrue();
        (larger >= equalToSmaller).Should().BeTrue();
    }

    [Fact]
    public void ComparisonOperator_WithDifferentCurrencies_ThrowsCurrencyMismatchException()
    {
        var act = () => new Money(10m, "HUF") < new Money(20m, "EUR");

        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Zero_ReturnsZeroAmountInGivenCurrency()
    {
        var zero = Money.Zero("EUR");

        zero.Should().Be(new Money(0m, "EUR"));
    }
}
