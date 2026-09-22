using FluentAssertions;
using Szamla.Application.Invoices.Queries;
using Szamla.Domain.Invoices;
using Szamla.Domain.Partners;
using Szamla.Infrastructure.Pdf;
using Xunit;

namespace Szamla.Infrastructure.Tests.Pdf;

/// <summary>No Docker/database needed: PDF rendering is pure in-memory composition.</summary>
public class InvoicePdfGeneratorTests
{
    // Normally set once in Szamla.Infrastructure.DependencyInjection.AddInfrastructure(); this
    // test exercises InvoicePdfGenerator directly, outside of DI, so it sets it itself.
    static InvoicePdfGeneratorTests() =>
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static InvoiceDto SampleInvoice() => new(
        Id: Guid.NewGuid(),
        Type: InvoiceType.Normal,
        Status: InvoiceStatus.Finalized,
        Number: "SZ-2026-000001",
        OriginalInvoiceId: null,
        IssueDate: new DateOnly(2026, 3, 1),
        PerformanceDate: new DateOnly(2026, 3, 1),
        PaymentDueDate: new DateOnly(2026, 3, 9),
        PaymentMethod: PaymentMethod.BankTransfer,
        Currency: "HUF",
        ExchangeRate: null,
        ExchangeRateSource: null,
        ExchangeRateDate: null,
        Issuer: new IssuerSnapshot("Kiállító Kft.", "12345678-1-42", "1011 Budapest, Fő utca 1.", "HU00-1111", false),
        Partner: new PartnerSnapshot("Vevő Kft.", false, PartnerCountryCategory.Domestic, "HU", "1012 Budapest, Vevő utca 2.", "87654321-1-42", null, null),
        Lines:
        [
            new InvoiceLineDto(Guid.NewGuid(), "Tanácsadás", 2m, "óra", 10000m, "27%", 20000m, 5400m, 25400m),
        ],
        NetTotal: 20000m,
        VatTotal: 5400m,
        GrossTotal: 25400m,
        VatTotalHufAmount: null,
        VatSummary: [new VatSummaryRowDto("27%", 20000m, 5400m, 25400m)]);

    [Fact]
    public void Generate_ProducesNonEmptyValidPdfBytes()
    {
        var generator = new InvoicePdfGenerator();

        var bytes = generator.Generate(SampleInvoice(), isCopy: false);

        bytes.Should().NotBeEmpty();
        // %PDF- magic bytes at the start of every valid PDF file.
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Generate_ForACopy_StillProducesValidPdfBytes()
    {
        var generator = new InvoicePdfGenerator();

        var bytes = generator.Generate(SampleInvoice(), isCopy: true);

        bytes.Should().NotBeEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }
}
