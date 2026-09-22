using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Invoices.Commands;

public sealed record CreateInvoiceSeriesCommand(Guid TenantId, string Prefix) : ICommand<Guid>;
