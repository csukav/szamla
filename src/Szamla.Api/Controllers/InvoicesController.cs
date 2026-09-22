using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szamla.Api.Contracts.Invoices;
using Szamla.Application.Common.Constants;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Interfaces;
using Szamla.Application.Invoices.Commands;
using Szamla.Application.Invoices.Queries;
using Szamla.Domain.Invoices;

namespace Szamla.Api.Controllers;

[Route("api/invoices")]
public sealed class InvoicesController(ISender sender, IInvoicePdfGenerator pdfGenerator) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceSummaryDto>>> List([FromQuery] InvoiceStatus? status, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ListInvoicesQuery(CurrentTenantId, status), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await sender.Send(new GetInvoiceQuery(CurrentTenantId, id), cancellationToken);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    [HttpPost]
    [Authorize(Roles = Roles.CanWrite)]
    public async Task<ActionResult<Guid>> CreateDraft(CreateDraftInvoiceRequest request, CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreateDraftInvoiceCommand(
                CurrentTenantId, request.PartnerId, request.IssueDate, request.PerformanceDate, request.PaymentDueDate,
                request.PaymentMethod, request.Currency, request.Lines.Select(l => l.ToApplicationInput()).ToList(),
                request.ExchangeRate, request.ExchangeRateSource, request.ExchangeRateDate),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("{id:guid}/lines")]
    [Authorize(Roles = Roles.CanWrite)]
    public async Task<IActionResult> ReplaceLines(Guid id, ReplaceInvoiceLinesRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(
            new ReplaceDraftInvoiceLinesCommand(CurrentTenantId, id, request.Lines.Select(l => l.ToApplicationInput()).ToList()),
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/finalize")]
    [Authorize(Roles = Roles.CanWrite)]
    public async Task<ActionResult<string>> Finalize(Guid id, FinalizeInvoiceRequest request, CancellationToken cancellationToken)
        => Ok(await sender.Send(new FinalizeInvoiceCommand(CurrentTenantId, id, request.InvoiceSeriesId), cancellationToken));

    [HttpPost("{id:guid}/storno")]
    [Authorize(Roles = Roles.CanWrite)]
    public async Task<ActionResult<Guid>> CreateStorno(Guid id, CreateStornoInvoiceRequest request, CancellationToken cancellationToken)
    {
        var stornoId = await sender.Send(new CreateStornoInvoiceCommand(CurrentTenantId, id, request.IssueDate), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = stornoId }, stornoId);
    }

    [HttpPost("{id:guid}/modify")]
    [Authorize(Roles = Roles.CanWrite)]
    public async Task<ActionResult<Guid>> CreateModification(Guid id, CreateModificationInvoiceRequest request, CancellationToken cancellationToken)
    {
        var modificationId = await sender.Send(
            new CreateModificationInvoiceCommand(
                CurrentTenantId, id, request.IssueDate, request.PerformanceDate, request.PaymentDueDate,
                request.PaymentMethod, request.CorrectionLines.Select(l => l.ToApplicationInput()).ToList()),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = modificationId }, modificationId);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, [FromQuery] bool copy, CancellationToken cancellationToken)
    {
        var invoice = await sender.Send(new GetInvoiceQuery(CurrentTenantId, id), cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        var pdfBytes = pdfGenerator.Generate(invoice, copy);
        var fileName = $"{invoice.Number ?? invoice.Id.ToString()}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}
