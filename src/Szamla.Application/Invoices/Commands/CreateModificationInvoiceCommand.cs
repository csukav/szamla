using Szamla.Application.Common.Cqrs;
using Szamla.Application.Invoices.Common;
using Szamla.Domain.Invoices;

namespace Szamla.Application.Invoices.Commands;

/// <summary>Creates a módosító (modification) draft referencing a finalized original — still needs its own FinalizeInvoiceCommand.</summary>
public sealed record CreateModificationInvoiceCommand(
    Guid TenantId,
    Guid OriginalInvoiceId,
    DateOnly IssueDate,
    DateOnly PerformanceDate,
    DateOnly PaymentDueDate,
    PaymentMethod PaymentMethod,
    IReadOnlyList<InvoiceLineInput> CorrectionLines) : ICommand<Guid>;
