using Szamla.Domain.Common;

namespace Szamla.Domain.Invoices;

public enum InvoiceStatus
{
    Draft,
    Finalized,
}

/// <summary>
/// The invoice aggregate: header, snapshot of issuer/partner as they were at issue time, lines,
/// and the totals/VAT breakdown computed from them. This phase covers the data shape and the VAT
/// arithmetic; the lifecycle transitions (draft -&gt; finalize with a real assigned Number,
/// storno/modification creation referencing OriginalInvoiceId) are added in the next phase, once
/// there is a persistence layer and a numbering service to drive them.
/// </summary>
public sealed class Invoice : AuditableEntity, ITenantScoped
{
    private readonly List<InvoiceLine> _lines;

    public Guid TenantId { get; }

    public InvoiceType Type { get; }

    public InvoiceStatus Status { get; private set; }

    /// <summary>Null until finalized — a draft never consumes a sequence number.</summary>
    public string? Number { get; private set; }

    /// <summary>Set for Storno/Modification invoices; points at the invoice being corrected.</summary>
    public Guid? OriginalInvoiceId { get; }

    public DateOnly IssueDate { get; }

    public DateOnly PerformanceDate { get; }

    public DateOnly PaymentDueDate { get; }

    public PaymentMethod PaymentMethod { get; }

    public string Currency { get; }

    /// <summary>HUF per one unit of <see cref="Currency"/> (MNB convention). Null for HUF invoices.</summary>
    public decimal? ExchangeRate { get; }

    public string? ExchangeRateSource { get; }

    public DateOnly? ExchangeRateDate { get; }

    public IssuerSnapshot Issuer { get; }

    public PartnerSnapshot Partner { get; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public Money NetTotal { get; }

    public Money VatTotal { get; }

    /// <summary>
    /// For a HUF invoice, rounded to whole forints per RoundingPolicy — so it will not always
    /// equal NetTotal + VatTotal exactly (see RoundingPolicy's remarks). For a foreign-currency
    /// invoice, this stays at 2-decimal precision in that currency; see VatTotalHuf for the
    /// legally required HUF-denominated VAT figure.
    /// </summary>
    public Money GrossTotal { get; }

    /// <summary>Required (non-null) whenever <see cref="Currency"/> isn't HUF: the VAT amount converted to and rounded to whole HUF, per the project brief.</summary>
    public Money? VatTotalHuf { get; }

    public IReadOnlyList<VatSummaryLine> VatSummary { get; }

    private Invoice(
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
        List<InvoiceLine> lines,
        Money netTotal,
        Money vatTotal,
        Money grossTotal,
        Money? vatTotalHuf,
        List<VatSummaryLine> vatSummary)
    {
        TenantId = tenantId;
        Type = type;
        Status = InvoiceStatus.Draft;
        OriginalInvoiceId = originalInvoiceId;
        IssueDate = issueDate;
        PerformanceDate = performanceDate;
        PaymentDueDate = paymentDueDate;
        PaymentMethod = paymentMethod;
        Currency = currency;
        ExchangeRate = exchangeRate;
        ExchangeRateSource = exchangeRateSource;
        ExchangeRateDate = exchangeRateDate;
        Issuer = issuer;
        Partner = partner;
        _lines = lines;
        NetTotal = netTotal;
        VatTotal = vatTotal;
        GrossTotal = grossTotal;
        VatTotalHuf = vatTotalHuf;
        VatSummary = vatSummary;
        CreatedAtUtc = DateTimeOffset.UtcNow;
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
        var lineList = lines.ToList();
        if (lineList.Count == 0)
        {
            throw new ArgumentException("A számlának legalább egy tételt kell tartalmaznia.", nameof(lines));
        }

        var normalizedCurrency = NormalizeCurrency(currency);

        if (lineList.Any(l => l.NetAmount.CurrencyCode != normalizedCurrency))
        {
            throw new ArgumentException("Minden tétel pénznemének meg kell egyeznie a számla pénznemével.", nameof(lines));
        }

        var isForeignCurrency = normalizedCurrency != "HUF";
        if (isForeignCurrency)
        {
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

        var netTotal = lineList.Aggregate(Money.Zero(normalizedCurrency), (sum, l) => sum + l.NetAmount);
        var vatTotal = lineList.Aggregate(Money.Zero(normalizedCurrency), (sum, l) => sum + l.VatAmount);
        var grossTotalRaw = netTotal + vatTotal;
        var grossTotal = normalizedCurrency == "HUF"
            ? new Money(RoundingPolicy.RoundHufTotal(grossTotalRaw.Amount), normalizedCurrency)
            : grossTotalRaw;

        Money? vatTotalHuf = isForeignCurrency
            ? new Money(RoundingPolicy.RoundHufTotal(vatTotal.Amount * exchangeRate!.Value), "HUF")
            : null;

        var vatSummary = lineList
            .GroupBy(l => l.VatRate)
            .Select(g => new VatSummaryLine(
                g.Key,
                g.Aggregate(Money.Zero(normalizedCurrency), (sum, l) => sum + l.NetAmount),
                g.Aggregate(Money.Zero(normalizedCurrency), (sum, l) => sum + l.VatAmount),
                g.Aggregate(Money.Zero(normalizedCurrency), (sum, l) => sum + l.GrossAmount)))
            .OrderBy(s => s.VatRate.DisplayCode, StringComparer.Ordinal)
            .ToList();

        return new Invoice(
            tenantId, type, originalInvoiceId,
            issueDate, performanceDate, paymentDueDate, paymentMethod,
            normalizedCurrency, exchangeRate, exchangeRateSource, exchangeRateDate,
            issuer, partner, lineList,
            netTotal, vatTotal, grossTotal, vatTotalHuf, vatSummary);
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
