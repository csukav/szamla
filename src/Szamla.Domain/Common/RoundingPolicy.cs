namespace Szamla.Domain.Common;

/// <summary>
/// The single place that defines every rounding rule used in invoice calculations, per the
/// project brief: line- and VAT-rate-level amounts keep 2 decimal places; only the invoice's
/// final HUF-denominated grand total (and the HUF-converted VAT amount shown on foreign-currency
/// invoices) round to whole forints. Rounding is "kerekítés" (arithmetic, away-from-zero at the
/// midpoint) throughout, not banker's rounding — .5 always rounds up in magnitude.
///
/// Because only the grand total is forint-rounded, NetTotal + VatTotal (both 2-decimal) will not
/// always add up to exactly GrossTotal in a HUF invoice; the difference (at most a few fillér) is
/// the rounding itself, not a bug. This mirrors how printed Hungarian invoices work.
/// </summary>
public static class RoundingPolicy
{
    private const int LineDecimalPlaces = 2;

    public static decimal RoundLineAmount(decimal amount) =>
        Math.Round(amount, LineDecimalPlaces, MidpointRounding.AwayFromZero);

    public static decimal RoundHufTotal(decimal amount) =>
        Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}
