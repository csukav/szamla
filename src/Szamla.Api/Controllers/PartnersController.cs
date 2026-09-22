using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szamla.Api.Contracts.Partners;
using Szamla.Application.Common.Constants;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Partners.Commands;
using Szamla.Application.Partners.Queries;

namespace Szamla.Api.Controllers;

[Route("api/partners")]
public sealed class PartnersController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PartnerDto>>> List([FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await sender.Send(new ListPartnersQuery(CurrentTenantId, search), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartnerDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var partner = await sender.Send(new GetPartnerQuery(CurrentTenantId, id), cancellationToken);
        return partner is null ? NotFound() : Ok(partner);
    }

    [HttpPost]
    [Authorize(Roles = Roles.CanWrite)]
    public async Task<ActionResult<Guid>> Create(CreatePartnerRequest request, CancellationToken cancellationToken)
    {
        var id = await sender.Send(
            new CreatePartnerCommand(
                CurrentTenantId, request.Name, request.IsPrivatePerson, request.CountryCategory, request.CountryCode,
                request.Address, request.TaxId, request.EuVatId, request.Email, request.PaymentTermDays),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.CanWrite)]
    public async Task<IActionResult> Update(Guid id, UpdatePartnerRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new UpdatePartnerCommand(CurrentTenantId, id, request.Address, request.Email, request.PaymentTermDays), cancellationToken);
        return NoContent();
    }
}
