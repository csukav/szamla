using Szamla.Domain.Common;

namespace Szamla.Domain.Invoices;

public enum InvoiceStatus
{
    Draft,
    Finalized,
}

/// <summary>
/// The invoice aggregate: header, snapshot of issuer/partner as they were at issue time, lines,
/// and the totals/VAT breakdown computed from them.
///
/// Immutability is structural, not bolted on: once <see cref="Finalize"/> has run, every mutating
/// method on this class throws (see <see cref="EnsureDraft"/>). A correction is always a new
/// invoice — <see cref="CreateStorno"/> or <see cref="CreateModification"/> — referencing this
/// one via <see cref="OriginalInvoiceId"/>, never an edit to the original.
///
/// Uses a parameterless constructor plus private setters (rather than one big constructor) even
/// though its factories look object-initializer-heavy: EF Core's constructor-binding
/// materialization can't bind owned-type or collection-typed constructor parameters (Issuer,
/// Partner, Lines here), so a real constructor covering every field isn't an option once this
/// type is mapped/persisted.
/// </summary>
public sealed class Invoice : AuditableEntity, ITenantScoped
{
    private List<InvoiceLine> _lines = [];

    public Guid TenantId { get; private set; }

    public InvoiceType Type { get; private set; }

    public InvoiceStatus Status { get; private set; }

    /// <summary>Null until finalized — a draft never consumes a sequence number.</summary>
    public string? Number { get; private set; }

    /// <summary>Set for Storno/Modification invoices; points at the invoice being corrected.</summary>
    public Guid? OriginalInvoiceId { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public DateOnly PerformanceDate { get; private set; }

    public DateOnly PaymentDueDate { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public string Currency { get; private set; } = default!;

    /// <summary>HUF per one unit of <see cref="Currency"/> (MNB convention). Null for HUF invoices.</summary>
    public decimal? ExchangeRate { get; private set; }

    public string? ExchangeRateSource { get; private set; }

    public DateOnly? ExchangeRateDate { get; private set; }

    public IssuerSnapshot Issuer { get; private set; } = default!;

    public PartnerSnapshot Partner { get; private set; } = default!;

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public Money NetTotal { get; private set; }

    public Money VatTotal { get; private set; }

    /// <summary>
    /// For a HUF invoice, rounded to whole forints per RoundingPolicy — so it will not always
    /// equal NetTotal + VatTotal exactly (see RoundingPolicy's remarks). For a foreign-currency
    /// invoice, this stays at 2-decimal precision in that currency; see VatTotalHufAmount for the
    /// legally required HUF-denominated VAT figure.
    /// </summary>
    public Money GrossTotal { get; private set; }

    /// <summary>Required (non-null) whenever <see cref="Currency"/> isn't HUF: the VAT amount converted to and rounded to whole HUF, per the project brief. Always HUF, so only the amount is stored.</summary>
    public decimal? VatTotalHufAmount { get; private set; }

    /// <summary>
    /// The mandatory per-VAT-rate breakdown table (áfa-kulcsonkénti bontás), grouped from the
    /// (already fixed, immutable-once-finalized) lines on every access rather than stored — safe
    /// because it only re-groups and sums figures that were computed once and never change,
    /// so there's no risk of it drifting from a future RoundingPolicy change the way storing and
    /// later recomputing raw totals from scratch would.
    /// </summary>
    public IReadOnlyList<VatSummaryLine> VatSummary => _lines
        .GroupBy(l => l.VatRate)
        .Select(g => new VatSummaryLine(
            g.Key,
            g.Aggregate(Money.Zero(Currency), (sum, l) => sum + l.NetAmount),
            g.Aggregate(Money.Zero(Currency), (sum, l) => sum + l.VatAmount),
            g.Aggregate(Money.Zero(Currency), (sum, l) => sum + l.GrossAmount)))
        .OrderBy(s => s.VatRate.DisplayCode, StringComparer.Ordinal)
        .ToList();

    private Invoice()
    {
    }

    public static Invoice CreateDraft(
        Guid tenantId,
        DateOnly issueDate,
        DateOnly performanceDate,
        DateOnly paymentDueDate,
        PaymentMethod paymentMethod,
        string currency,
        IssuerSnapshot issuer,
        PartnerSnapshot partner,
        IEnumerable<InvoiceLine> lines,
        InvoiceType type = InvoiceType.Normal,
        Guid? originalInvoiceId = null,
        decimal? exchangeRate = null,
        string? exchangeRateSource = null,
        DateOnly? exchangeRateDate = null)
    {
        var normalizedCurrency = NormalizeCurrency(currency);
        var lineList = ValidateLines(lines, normalizedCurrency);
        ValidateExchangeRateInputs(normalizedCurrency, exchangeRate, exchangeRateSource, exchangeRateDate);

        return Construct(
            tenantId, type, originalInvoiceId,
            issueDate, performanceDate, paymentDueDate, paymentMethod,
            normalizedCurrency, exchangeRate, exchangeRateSource, exchangeRateDate,
            issuer, partner, lineList);
    }

    /// <summary>Replaces the line items of a draft invoice and recomputes its totals. Only valid while <see cref="Status"/> is Draft.</summary>
    public void ReplaceLines(IEnumerable<InvoiceLine> newLines)
    {
        EnsureDraft();

        var lineList = ValidateLines(newLines, Currency);
        _lines.Clear();
        _lines.AddRange(lineList);
        RecomputeTotals();
        ModifiedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Updates the editable header fields of a draft invoice. Only valid while <see cref="Status"/> is Draft.</summary>
    public void UpdateHeader(DateOnly issueDate, DateOnly performanceDate, DateOnly paymentDueDate, PaymentMethod paymentMethod)
    {
        EnsureDraft();

        IssueDate = issueDate;
        PerformanceDate = performanceDate;
        PaymentDueDate = paymentDueDate;
        PaymentMethod = paymentMethod;
        ModifiedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Assigns the sequence number and marks the invoice Finalized — irreversible. The caller is
    /// responsible for having obtained <paramref name="number"/> from the concurrency-safe
    /// IInvoiceNumberGenerator; this method only enforces the invoice-side invariants (must
    /// currently be a draft, a number must be supplied).
    /// </summary>
    public void Finalize(string number, DateTimeOffset finalizedAtUtc)
    {
        EnsureDraft();

        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException("A számlaszám kötelező a véglegesítéshez.", nameof(number));
        }

        Number = number.Trim();
        Status = InvoiceStatus.Finalized;
        ModifiedAtUtc = finalizedAtUtc;
    }

    /// <summary>
    /// Creates the storno (technical cancellation) of a finalized invoice: a new draft with every
    /// line mirrored at negated quantity, so its totals exactly cancel the original's. Like any
    /// other invoice, it still needs its own <see cref="Finalize"/> call (and its own sequence
    /// number) to become legally effective.
    /// </summary>
    public static Invoice CreateStorno(Invoice original, DateOnly issueDate)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (original.Status != InvoiceStatus.Finalized)
        {
            throw new InvalidOperationException("Csak véglegesített számla sztornózható.");
        }

        if (original.Type == InvoiceType.Storno)
        {
            throw new InvalidOperationException("Sztornó számla nem sztornózható.");
        }

        var mirroredLines = original.Lines.Select(InvoiceLine.CreateStornoMirror).ToList();

        // `with { }` copies, not the original's own instances: IssuerSnapshot/PartnerSnapshot are
        // EF Core owned types keyed by their owning Invoice's id, so sharing the exact same
        // instance between two Invoice rows (original and this new one) breaks persistence — EF
        // can't attach one owned-type instance to two different owners.
        return Construct(
            original.TenantId, InvoiceType.Storno, original.Id,
            issueDate, issueDate, issueDate, original.PaymentMethod,
            original.Currency, original.ExchangeRate, original.ExchangeRateSource, original.ExchangeRateDate,
            original.Issuer with { }, original.Partner with { }, mirroredLines);
    }

    /// <summary>
    /// Creates a modification (módosító) invoice referencing a finalized original. Unlike Storno,
    /// the correction lines are supplied by the caller rather than derived automatically — Phase
    /// 4 deliberately leaves the delta-vs-full-replacement accounting convention as an Application
    /// layer/UI decision rather than assuming one, since Hungarian practice varies and this should
    /// be confirmed with the accountant before the correction workflow ships.
    /// </summary>
    public static Invoice CreateModification(
        Invoice original,
        DateOnly issueDate,
        DateOnly performanceDate,
        DateOnly paymentDueDate,
        PaymentMethod paymentMethod,
        IEnumerable<InvoiceLine> correctionLines)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (original.Status != InvoiceStatus.Finalized)
        {
            throw new InvalidOperationException("Csak véglegesített számla módosítható.");
        }

        if (original.Type == InvoiceType.Storno)
        {
            throw new InvalidOperationException("Sztornó számla nem módosítható.");
        }

        var lineList = ValidateLines(correctionLines, original.Currency);

        // See the same `with { }` remark in CreateStorno just above.
        return Construct(
            original.TenantId, InvoiceType.Modification, original.Id,
            issueDate, performanceDate, paymentDueDate, paymentMethod,
            original.Currency, original.ExchangeRate, original.ExchangeRateSource, original.ExchangeRateDate,
            original.Issuer with { }, original.Partner with { }, lineList);
    }

    private static Invoice Construct(
        Guid tenantId,
        InvoiceType type,
        Guid? originalInvoiceId,
        DateOnly issueDate,
        DateOnly performanceDate,
        DateOnly paymentDueDate,
        PaymentMethod paymentMethod,
        string currency,
        decimal? exchangeRate,
        string? exchangeRateSource,
        DateOnly? exchangeRateDate,
        IssuerSnapshot issuer,
        PartnerSnapshot partner,
        List<InvoiceLine> lines)
    {
        var invoice = new Invoice
        {
            TenantId = tenantId,
            Type = type,
            Status = InvoiceStatus.Draft,
            OriginalInvoiceId = originalInvoiceId,
            IssueDate = issueDate,
            PerformanceDate = performanceDate,
            PaymentDueDate = paymentDueDate,
            PaymentMethod = paymentMethod,
            Currency = currency,
            ExchangeRate = exchangeRate,
            ExchangeRateSource = exchangeRateSource,
            ExchangeRateDate = exchangeRateDate,
            Issuer = issuer,
            Partner = partner,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        invoice._lines = lines;
        invoice.RecomputeTotals();

        return invoice;
    }

    private void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("A véglegesített számla nem módosítható vagy törölhető — javítás csak sztornó vagy módosító számlával lehetséges.");
        }
    }

    private void RecomputeTotals()
    {
        var netTotal = _lines.Aggregate(Money.Zero(Currency), (sum, l) => sum + l.NetAmount);
        var vatTotal = _lines.Aggregate(Money.Zero(Currency), (sum, l) => sum + l.VatAmount);
        var grossTotalRaw = netTotal + vatTotal;

        NetTotal = netTotal;
        VatTotal = vatTotal;
        GrossTotal = Currency == "HUF"
            ? new Money(RoundingPolicy.RoundHufTotal(grossTotalRaw.Amount), Currency)
            : grossTotalRaw;
        VatTotalHufAmount = Currency != "HUF"
            ? RoundingPolicy.RoundHufTotal(vatTotal.Amount * ExchangeRate!.Value)
            : null;
    }

    private static List<InvoiceLine> ValidateLines(IEnumerable<InvoiceLine> lines, string currency)
    {
        var lineList = lines.ToList();
        if (lineList.Count == 0)
        {
            throw new ArgumentException("A számlának legalább egy tételt kell tartalmaznia.", nameof(lines));
        }

        if (lineList.Any(l => l.NetAmount.CurrencyCode != currency))
        {
            throw new ArgumentException("Minden tétel pénznemének meg kell egyeznie a számla pénznemével.", nameof(lines));
        }

        return lineList;
    }

    private static void ValidateExchangeRateInputs(string currency, decimal? exchangeRate, string? exchangeRateSource, DateOnly? exchangeRateDate)
    {
        if (currency == "HUF")
        {
            return;
        }

        if (exchangeRate is not > 0)
        {
            throw new ArgumentException("Devizás számlánál az árfolyamot meg kell adni.", nameof(exchangeRate));
        }

        if (string.IsNullOrWhiteSpace(exchangeRateSource))
        {
            throw new ArgumentException("Devizás számlánál az árfolyam forrását meg kell adni.", nameof(exchangeRateSource));
        }

        if (exchangeRateDate is null)
        {
            throw new ArgumentException("Devizás számlánál az árfolyam dátumát meg kell adni.", nameof(exchangeRateDate));
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("A pénznemnek ISO 4217 hárombetűs kódnak kell lennie.", nameof(currency));
        }

        return currency.Trim().ToUpperInvariant();
    }
}
