using Szamla.Application.Common.Cqrs;
using Szamla.Application.Invoices.Common;

namespace Szamla.Application.Invoices.Commands;

public sealed record ReplaceDraftInvoiceLinesCommand(
    Guid TenantId,
    Guid InvoiceId,
    IReadOnlyList<InvoiceLineInput> Lines) : ICommand<Guid>;
