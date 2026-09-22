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

        invoice.VatTotalHuf.Should().Be(new Money(10564m, "HUF"));
        invoice.GrossTotal.Should().Be(new Money(127.00m, "EUR")); // foreign-currency total is NOT rounded to whole units
    }
}
