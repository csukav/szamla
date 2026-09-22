namespace Szamla.Domain.Common.Exceptions;

/// <summary>Thrown when an operation is attempted between two Money values of different currencies.</summary>
public sealed class CurrencyMismatchException(string leftCurrency, string rightCurrency)
    : Exception($"Nem végezhető művelet eltérő pénznemű összegeken: {leftCurrency} és {rightCurrency}.");
