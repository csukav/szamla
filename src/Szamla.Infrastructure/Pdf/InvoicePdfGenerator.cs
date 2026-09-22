using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Szamla.Application.Common.Interfaces;
using Szamla.Application.Invoices.Queries;
using Szamla.Domain.Invoices;

namespace Szamla.Infrastructure.Pdf;

/// <summary>
/// Renders the invoice content elements generally required on a Hungarian invoice (kiállító és
/// vevő adatai, számlaszám, dátumok, tételek, ÁFA-bontás, végösszegek) via QuestPDF. This is a
/// functional, unbranded layout for the MVP — a legal completeness review (exact required
/// wording, any further mandatory fields for specific cases such as fordított adózás or EU
/// supplies) is still owed before this is relied on for real invoicing, per the project brief's
/// own instruction not to trust memory on legal specifics.
/// </summary>
public sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    public byte[] Generate(InvoiceDto invoice, bool isCopy)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(c => ComposeHeader(c, invoice, isCopy));
                page.Content().Element(c => ComposeContent(c, invoice));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Oldal ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, InvoiceDto invoice, bool isCopy)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(TitleFor(invoice.Type)).FontSize(18).Bold();
                if (isCopy)
                {
                    row.ConstantItem(100).AlignRight().Text("MÁSOLAT").FontSize(14).Bold().FontColor(Colors.Red.Medium);
                }
            });
            column.Item().Text($"Számlaszám: {invoice.Number ?? "PISZKOZAT"}").FontSize(12);
            if (invoice.OriginalInvoiceId is not null)
            {
                column.Item().Text($"Eredeti számla: {invoice.OriginalInvoiceId}").FontSize(9);
            }
        });
    }

    private static string TitleFor(InvoiceType type) => type switch
    {
        InvoiceType.Normal => "SZÁMLA",
        InvoiceType.Storno => "SZTORNÓ SZÁMLA",
        InvoiceType.Modification => "MÓDOSÍTÓ SZÁMLA",
        _ => "SZÁMLA",
    };

    private static void ComposeContent(IContainer container, InvoiceDto invoice)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Spacing(10);

            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => ComposePartyBlock(c, "Kiállító", invoice.Issuer.Name, invoice.Issuer.TaxId, invoice.Issuer.Address, invoice.Issuer.BankAccount));
                row.RelativeItem().Element(c => ComposePartyBlock(c, "Vevő", invoice.Partner.Name, invoice.Partner.TaxId ?? invoice.Partner.EuVatId, invoice.Partner.Address, null));
            });

            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"Kiállítás dátuma: {invoice.IssueDate:yyyy.MM.dd.}");
                row.RelativeItem().Text($"Teljesítés dátuma: {invoice.PerformanceDate:yyyy.MM.dd.}");
                row.RelativeItem().Text($"Fizetési határidő: {invoice.PaymentDueDate:yyyy.MM.dd.}");
                row.RelativeItem().Text($"Fizetési mód: {PaymentMethodLabel(invoice.PaymentMethod)}");
            });

            column.Item().Element(c => ComposeLinesTable(c, invoice));
            column.Item().Element(c => ComposeVatSummaryTable(c, invoice));
            column.Item().Element(c => ComposeTotals(c, invoice));
        });
    }

    private static void ComposePartyBlock(IContainer container, string title, string name, string? taxId, string address, string? bankAccount)
    {
        container.Column(column =>
        {
            column.Item().Text(title).Bold();
            column.Item().Text(name);
            column.Item().Text(address);
            if (!string.IsNullOrWhiteSpace(taxId))
            {
                column.Item().Text($"Adószám: {taxId}");
            }
            if (!string.IsNullOrWhiteSpace(bankAccount))
            {
                column.Item().Text($"Bankszámlaszám: {bankAccount}");
            }
        });
    }

    private static string PaymentMethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.BankTransfer => "átutalás",
        PaymentMethod.Cash => "készpénz",
        PaymentMethod.Card => "bankkártya",
        _ => method.ToString(),
    };

    private static void ComposeLinesTable(IContainer container, InvoiceDto invoice)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
            });

            table.Header(header =>
            {
                foreach (var text in new[] { "Megnevezés", "Menny.", "Egys.", "Nettó egységár", "ÁFA", "Nettó", "Bruttó" })
                {
                    header.Cell().Element(HeaderCellStyle).Text(text).Bold();
                }
            });

            foreach (var line in invoice.Lines)
            {
                table.Cell().Element(BodyCellStyle).Text(line.Description);
                table.Cell().Element(BodyCellStyle).AlignRight().Text($"{line.Quantity}");
                table.Cell().Element(BodyCellStyle).Text(line.Unit);
                table.Cell().Element(BodyCellStyle).AlignRight().Text($"{line.NetUnitPriceAmount:N2}");
                table.Cell().Element(BodyCellStyle).AlignRight().Text(line.VatRateDisplayCode);
                table.Cell().Element(BodyCellStyle).AlignRight().Text($"{line.NetAmount:N2}");
                table.Cell().Element(BodyCellStyle).AlignRight().Text($"{line.GrossAmount:N2}");
            }
        });

        static IContainer HeaderCellStyle(IContainer c) => c.Background(Colors.Grey.Lighten3).Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1);
        static IContainer BodyCellStyle(IContainer c) => c.Padding(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1);
    }

    private static void ComposeVatSummaryTable(IContainer container, InvoiceDto invoice)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
            });

            table.Header(header =>
            {
                foreach (var text in new[] { "ÁFA kulcs", "Nettó", "ÁFA", "Bruttó" })
                {
                    header.Cell().Padding(4).BorderBottom(1).BorderColor(Colors.Grey.Darken1).Text(text).Bold();
                }
            });

            foreach (var row in invoice.VatSummary)
            {
                table.Cell().Padding(4).Text(row.VatRateDisplayCode);
                table.Cell().Padding(4).AlignRight().Text($"{row.NetAmount:N2}");
                table.Cell().Padding(4).AlignRight().Text($"{row.VatAmount:N2}");
                table.Cell().Padding(4).AlignRight().Text($"{row.GrossAmount:N2}");
            }
        });
    }

    private static void ComposeTotals(IContainer container, InvoiceDto invoice)
    {
        container.AlignRight().Column(column =>
        {
            column.Item().Text($"Nettó végösszeg: {invoice.NetTotal:N2} {invoice.Currency}");
            column.Item().Text($"ÁFA végösszeg: {invoice.VatTotal:N2} {invoice.Currency}");
            column.Item().Text($"Fizetendő végösszeg: {invoice.GrossTotal:N2} {invoice.Currency}").Bold().FontSize(12);

            if (invoice.VatTotalHufAmount is not null)
            {
                column.Item().Text($"ÁFA végösszeg forintban: {invoice.VatTotalHufAmount:N0} HUF");
                column.Item().Text($"Árfolyam: {invoice.ExchangeRate} ({invoice.ExchangeRateSource}, {invoice.ExchangeRateDate:yyyy.MM.dd.})").FontSize(8);
            }
        });
    }
}
