using FluentAssertions;
using Szamla.Domain.Common;
using Xunit;

namespace Szamla.Domain.Tests.Common;

public class RoundingPolicyTests
{
    [Theory]
    [InlineData(10.123, 10.12)]
    [InlineData(10.125, 10.13)] // away-from-zero at the midpoint, not banker's rounding
    [InlineData(10.126, 10.13)]
    [InlineData(-10.125, -10.13)]
    public void RoundLineAmount_RoundsToTwoDecimalsAwayFromZero(decimal input, decimal expected)
    {
        RoundingPolicy.RoundLineAmount(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(1000.49, 1000)]
    [InlineData(1000.50, 1001)] // away-from-zero at the midpoint
    [InlineData(1000.51, 1001)]
    public void RoundHufTotal_RoundsToWholeForintAwayFromZero(decimal input, decimal expected)
    {
        RoundingPolicy.RoundHufTotal(input).Should().Be(expected);
    }
}
