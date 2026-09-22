using FluentAssertions;
using Szamla.Domain.Common;
using Szamla.Domain.Invoices;
using Szamla.Domain.Partners;
using Xunit;

namespace Szamla.Domain.Tests.Invoices;

public class InvoiceTests
{
    private static readonly IssuerSnapshot Issuer = new("Kiállító Kft.", "12345678-1-42", "1011 Budapest, Fő utca 1.", "HU00-1111", false);
    private static readonly PartnerSnapshot DomesticCompany = new("Vevő Kft.", false, PartnerCountryCategory.Domestic, "HU", "1012 Budapest, Vevő utca 2.", "87654321-1-42", null, null);

    private static readonly DateOnly Today = new(2026, 3, 15);

    [Fact]
    public void CreateDraft_WithHufLines_SumsNetAndVatTotalsExactly()
    {
        var lines = new[]
        {
            InvoiceLine.Create("Tétel 1", 1m, "db", new Money(10000m, "HUF"), VatRate.OfPercentage(0.27m)),
            InvoiceLine.Create("Tétel 2", 1m, "db", new Money(5000m, "HUF"), VatRate.OfPercentage(0.05m)),
        };

        var invoice = Invoice.CreateDraft(
            Guid.NewGuid(), Today, Today, Today.AddDays(8), PaymentMethod.BankTransfer,
            "HUF", Issuer, DomesticCompany, lines);

        invoice.NetTotal.Should().Be(new Money(15000m, "HUF"));
        invoice.VatTotal.Should().Be(new Money(2950m, "HUF")); // 2700 + 250
        invoice.GrossTotal.Should().Be(new Money(17950m, "HUF"));
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.Number.Should().BeNull();
    }

    [Fact]
    public void CreateDraft_GroupsLinesIntoVatSummaryByRate()
    {
        var lines = new[]
        {
            InvoiceLine.Create("Tétel A", 1m, "db", new Money(1000m, "HUF"), VatRate.OfPercentage(0.27m)),
            InvoiceLine.Create("Tétel B", 1m, "db", new Money(2000m, "HUF"), VatRate.OfPercentage(0.27m)),
            InvoiceLine.Create("Tétel C", 1m, "db", new Money(500m, "HUF"), VatRate.Exempt(VatExemptionReason.SubjectExempt)),
        };

        var invoice = Invoice.CreateDraft(
            Guid.NewGuid(), Today, Today, Today.AddDays(8), PaymentMethod.BankTransfer,
            "HUF", Issuer, DomesticCompany, lines);

        invoice.VatSummary.Should().HaveCount(2);
        var twentySevenPercentRow = invoice.VatSummary.Single(r => r.VatRate.Equals(VatRate.OfPercentage(0.27m)));
        twentySevenPercentRow.NetAmount.Should().Be(new Money(3000m, "HUF"));
        twentySevenPercentRow.VatAmount.Should().Be(new Money(810m, "HUF"));

        var exemptRow = invoice.VatSummary.Single(r => r.VatRate.Kind == VatRateKind.Exempt);
        exemptRow.NetAmount.Should().Be(new Money(500m, "HUF"));
        exemptRow.VatAmount.Should().Be(Money.Zero("HUF"));
    }

    [Fact]
    public void CreateDraft_WithHufTotalRequiringRounding_RoundsGrossTotalToWholeForint()
    {
        // A single line whose gross amount already has a fractional forint (net+vat rounded
        // independently can leave a fillér-level remainder at the total).
        var lines = new[]
        {
            InvoiceLine.Create("Tétel", 1m, "db", new Money(100.50m, "HUF"), VatRate.OfPercentage(0.05m)),
        };
        // net = 100.50, vat = round(100.50*0.05=5.025) = 5.03(away-from-zero), gross raw = 105.53

        var invoice = Invoice.CreateDraft(
            Guid.NewGuid(), Today, Today, Today.AddDays(8), PaymentMethod.Cash,
            "HUF", Issuer, DomesticCompany, lines);

        invoice.GrossTotal.Should().Be(new Money(106m, "HUF")); // 105.53 rounds to 106
    }

    [Fact]
    public void CreateDraft_WithNoLines_Throws()
    {
        var act = () => Invoice.CreateDraft(
            Guid.NewGuid(), Today, Today, Today.AddDays(8), PaymentMethod.BankTransfer,
            "HUF", Issuer, DomesticCompany, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_WithLineCurrencyMismatchingInvoiceCurrency_Throws()
    {
        var lines = new[] { InvoiceLine.Create("Tétel", 1m, "db", new Money(100m, "EUR"), VatRate.OfPercentage(0.27m)) };

        var act = () => Invoice.CreateDraft(
            Guid.NewGuid(), Today, Today, Today.AddDays(8), PaymentMethod.BankTransfer,
            "HUF", Issuer, DomesticCompany, lines);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_ForeignCurrency_WithoutExchangeRate_Throws()
    {
        var lines = new[] { InvoiceLine.Create("Tétel", 1m, "db", new Money(100m, "EUR"), VatRate.OfPercentage(0.27m)) };

        var act = () => Invoice.CreateDraft(
            Guid.NewGuid(), Today, Today, Today.AddDays(8), PaymentMethod.BankTransfer,
            "EUR", Issuer, DomesticCompany, lines);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateDraft_ForeignCurrency_WithExchangeRate_ComputesVatTotalHufRoundedToWholeForint()
    {
        var lines = new[] { InvoiceLine.Create("Tétel", 1m, "db", new Money(100m, "EUR"), VatRate.OfPercentage(0.27m)) };
        // vatTotal = 27.00 EUR; exchangeRate = 391.256 HUF/EUR -> 27 * 391.256 = 10563.912 -> rounds to 10564

        var invoice = Invoice.CreateDraft(
            Guid.NewGuid(), Today, Today, Today.AddDays(8), PaymentMethod.BankTransfer,
            "EUR", Issuer, DomesticCompany, lines,
            exchangeRate: 391.256m, exchangeRateSource: "MNB", exchangeRateDate: Today);

        invoice.VatTotalHufAmount.Should().Be(10564m);
        invoice.GrossTotal.Should().Be(new Money(127.00m, "EUR")); // foreign-currency total is NOT rounded to whole units
    }

    private static Invoice CreateHufDraft(params InvoiceLine[] lines) => Invoice.CreateDraft(
        Guid.NewGuid(), Today, Today, Today.AddDays(8), PaymentMethod.BankTransfer, "HUF", Issuer, DomesticCompany,
        lines.Length == 0 ? [InvoiceLine.Create("Tétel", 1m, "db", new Money(1000m, "HUF"), VatRate.OfPercentage(0.27m))] : lines);

    [Fact]
    public void Finalize_OnDraft_AssignsNumberAndSetsFinalizedStatus()
    {
        var invoice = CreateHufDraft();
        var finalizedAt = new DateTimeOffset(2026, 3, 16, 10, 0, 0, TimeSpan.Zero);

        invoice.Finalize("SZ-2026-000001", finalizedAt);

        invoice.Status.Should().Be(InvoiceStatus.Finalized);
        invoice.Number.Should().Be("SZ-2026-000001");
        invoice.ModifiedAtUtc.Should().Be(finalizedAt);
    }

    [Fact]
    public void Finalize_CalledTwice_ThrowsOnSecondCall()
    {
        var invoice = CreateHufDraft();
        invoice.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow);

        var act = () => invoice.Finalize("SZ-2026-000002", DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Finalize_WithEmptyNumber_Throws()
    {
        var invoice = CreateHufDraft();

        var act = () => invoice.Finalize("", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReplaceLines_OnDraft_RecomputesTotals()
    {
        var invoice = CreateHufDraft(InvoiceLine.Create("Régi", 1m, "db", new Money(1000m, "HUF"), VatRate.OfPercentage(0.27m)));

        invoice.ReplaceLines([InvoiceLine.Create("Új", 2m, "db", new Money(500m, "HUF"), VatRate.OfPercentage(0.05m))]);

        invoice.NetTotal.Should().Be(new Money(1000m, "HUF"));
        invoice.VatTotal.Should().Be(new Money(50m, "HUF"));
        invoice.Lines.Should().ContainSingle(l => l.Description == "Új");
    }

    [Fact]
    public void ReplaceLines_OnFinalizedInvoice_Throws()
    {
        var invoice = CreateHufDraft();
        invoice.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow);

        var act = () => invoice.ReplaceLines([InvoiceLine.Create("Új", 1m, "db", new Money(100m, "HUF"), VatRate.OfPercentage(0.27m))]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateHeader_OnFinalizedInvoice_Throws()
    {
        var invoice = CreateHufDraft();
        invoice.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow);

        var act = () => invoice.UpdateHeader(Today, Today, Today.AddDays(30), PaymentMethod.Cash);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateStorno_OfFinalizedInvoice_MirrorsLinesWithNegatedAmountsAndCancelsToZero()
    {
        var original = CreateHufDraft(InvoiceLine.Create("Tétel", 3m, "db", new Money(1000m, "HUF"), VatRate.OfPercentage(0.27m)));
        original.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow);

        var storno = Invoice.CreateStorno(original, Today.AddDays(1));

        storno.Type.Should().Be(InvoiceType.Storno);
        storno.OriginalInvoiceId.Should().Be(original.Id);
        storno.Status.Should().Be(InvoiceStatus.Draft);
        storno.NetTotal.Should().Be(new Money(-3000m, "HUF"));
        (original.NetTotal + storno.NetTotal).Should().Be(Money.Zero("HUF"));
        (original.GrossTotal + storno.GrossTotal).Should().Be(Money.Zero("HUF"));
    }

    [Fact]
    public void CreateStorno_CopiesIssuerAndPartnerRatherThanSharingTheOriginalsInstances()
    {
        // EF Core owned types (Issuer/Partner) are keyed by their owning Invoice's id, so sharing
        // one instance between two Invoice rows breaks persistence — regression test for that bug.
        var original = CreateHufDraft();
        original.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow);

        var storno = Invoice.CreateStorno(original, Today.AddDays(1));

        ReferenceEquals(original.Issuer, storno.Issuer).Should().BeFalse();
        ReferenceEquals(original.Partner, storno.Partner).Should().BeFalse();
        storno.Issuer.Should().Be(original.Issuer);
        storno.Partner.Should().Be(original.Partner);
    }

    [Fact]
    public void CreateStorno_OfDraftInvoice_Throws()
    {
        var draft = CreateHufDraft();

        var act = () => Invoice.CreateStorno(draft, Today);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateStorno_OfAlreadyStornoInvoice_Throws()
    {
        var original = CreateHufDraft();
        original.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow);
        var storno = Invoice.CreateStorno(original, Today.AddDays(1));
        storno.Finalize("SZ-2026-000002", DateTimeOffset.UtcNow);

        var act = () => Invoice.CreateStorno(storno, Today.AddDays(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateModification_OfFinalizedInvoice_ReferencesOriginalAndUsesSuppliedLines()
    {
        var original = CreateHufDraft();
        original.Finalize("SZ-2026-000001", DateTimeOffset.UtcNow);
        var correctionLines = new[] { InvoiceLine.Create("Korrekció", 1m, "db", new Money(200m, "HUF"), VatRate.OfPercentage(0.27m)) };

        var modification = Invoice.CreateModification(original, Today.AddDays(1), Today.AddDays(1), Today.AddDays(9), PaymentMethod.BankTransfer, correctionLines);

        modification.Type.Should().Be(InvoiceType.Modification);
        modification.OriginalInvoiceId.Should().Be(original.Id);
        modification.NetTotal.Should().Be(new Money(200m, "HUF"));
    }

    [Fact]
    public void CreateModification_OfDraftInvoice_Throws()
    {
        var draft = CreateHufDraft();
        var correctionLines = new[] { InvoiceLine.Create("Korrekció", 1m, "db", new Money(200m, "HUF"), VatRate.OfPercentage(0.27m)) };

        var act = () => Invoice.CreateModification(draft, Today, Today, Today.AddDays(8), PaymentMethod.BankTransfer, correctionLines);

        act.Should().Throw<InvalidOperationException>();
    }
}
