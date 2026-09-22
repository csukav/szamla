using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Invoices.Commands;

/// <summary>Creates the storno (technical cancellation) draft of a finalized invoice — still needs its own FinalizeInvoiceCommand to become legally effective.</summary>
public sealed record CreateStornoInvoiceCommand(Guid TenantId, Guid OriginalInvoiceId, DateOnly IssueDate) : ICommand<Guid>;
