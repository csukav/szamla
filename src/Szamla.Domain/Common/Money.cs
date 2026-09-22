using Szamla.Domain.Common.Exceptions;

namespace Szamla.Domain.Common;

/// <summary>
/// An amount tied to a specific currency. All arithmetic requires both operands to share a
/// currency — mixing HUF and EUR silently would be a bug, not a convenience, so it throws instead.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }

    public string CurrencyCode { get; }

    public Money(decimal amount, string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
        {
            throw new ArgumentException("A pénznemnek ISO 4217 hárombetűs kódnak kell lennie.", nameof(currencyCode));
        }

        Amount = amount;
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currencyCode) => new(0m, currencyCode);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.CurrencyCode);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.CurrencyCode);
    }

    public static Money operator -(Money value) => new(-value.Amount, value.CurrencyCode);

    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor, money.CurrencyCode);

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.CurrencyCode != right.CurrencyCode)
        {
            throw new CurrencyMismatchException(left.CurrencyCode, right.CurrencyCode);
        }
    }

    public override string ToString() => $"{Amount} {CurrencyCode}";
}
