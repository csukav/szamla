using Szamla.Application.Invoices.Queries;

namespace Szamla.Application.Common.Interfaces;

/// <summary>Renders an invoice to a PDF matching the mandatory Hungarian invoice content. Set isCopy for any printout after the first — those must be watermarked "MÁSOLAT".</summary>
public interface IInvoicePdfGenerator
{
    byte[] Generate(InvoiceDto invoice, bool isCopy);
}
