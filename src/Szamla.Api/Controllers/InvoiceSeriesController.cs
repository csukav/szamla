using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szamla.Api.Contracts.Invoices;
using Szamla.Application.Common.Constants;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Invoices.Commands;

namespace Szamla.Api.Controllers;

[Route("api/invoice-series")]
public sealed class InvoiceSeriesController(ISender sender) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.CanManageSettings)]
    public async Task<ActionResult<Guid>> Create(CreateInvoiceSeriesRequest request, CancellationToken cancellationToken)
    {
        var id = await sender.Send(new CreateInvoiceSeriesCommand(CurrentTenantId, request.Prefix), cancellationToken);
        return Created($"/api/invoice-series/{id}", id);
    }
}
