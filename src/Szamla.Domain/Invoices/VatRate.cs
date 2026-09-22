namespace Szamla.Domain.Invoices;

public enum VatRateKind
{
    Percentage,
    Exempt,
}

/// <summary>
/// Either a percentage rate (27%, 18%, 5%, 0%) or one of the VAT-exemption reasons — exactly one
/// of <see cref="Percentage"/>/<see cref="ExemptionReason"/> is set, matching <see cref="Kind"/>.
/// Value-equal (two instances with the same Kind/Percentage/ExemptionReason are equal), which
/// matters for grouping invoice lines into the mandatory per-rate VAT summary table.
/// </summary>
public sealed class VatRate : IEquatable<VatRate>
{
    private static readonly decimal[] AllowedPercentages = [0.27m, 0.18m, 0.05m, 0.00m];

    public VatRateKind Kind { get; private set; }

    public decimal? Percentage { get; private set; }

    public VatExemptionReason? ExemptionReason { get; private set; }

    // Parameterless + private setters, not a constructor taking all three: EF Core's
    // ComplexProperty materialization needs a constructor it can bind purely from mapped
    // properties, and a private constructor here wasn't picked up as one (see Invoice's
    // remarks on the same constraint for owned/complex types).
    private VatRate()
    {
    }

    public static VatRate OfPercentage(decimal percentage)
    {
        if (!AllowedPercentages.Contains(percentage))
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), percentage,
                "Csak a 27%, 18%, 5% vagy 0% ÁFA-kulcs támogatott.");
        }

        return new VatRate { Kind = VatRateKind.Percentage, Percentage = percentage };
    }

    public static VatRate Exempt(VatExemptionReason reason) =>
        new() { Kind = VatRateKind.Exempt, ExemptionReason = reason };

    /// <summary>True when no VAT is payable — either an exemption, or the 0% rate.</summary>
    public bool IsZeroLiability => Kind == VatRateKind.Exempt || Percentage == 0.00m;

    /// <summary>The short code shown on the invoice's VAT-rate breakdown table.</summary>
    public string DisplayCode => Kind switch
    {
        VatRateKind.Percentage => $"{Percentage:0%}",
        VatRateKind.Exempt => ExemptionReason!.Value switch
        {
            VatExemptionReason.SubjectExempt => "AAM",
            VatExemptionReason.ObjectExempt => "TAM",
            VatExemptionReason.IntraCommunitySupply => "EUE",
            VatExemptionReason.IntraCommunityReverseCharge => "EUFAD37",
            VatExemptionReason.DomesticReverseCharge => "ÁFA-FORDÍTOTT",
            VatExemptionReason.OutsideVatScope => "EU-N-KÍVÜLI",
            _ => throw new ArgumentOutOfRangeException(),
        },
        _ => throw new ArgumentOutOfRangeException(),
    };

    public bool Equals(VatRate? other) =>
        other is not null && Kind == other.Kind && Percentage == other.Percentage && ExemptionReason == other.ExemptionReason;

    public override bool Equals(object? obj) => Equals(obj as VatRate);

    public override int GetHashCode() => HashCode.Combine(Kind, Percentage, ExemptionReason);

    public override string ToString() => DisplayCode;
}
