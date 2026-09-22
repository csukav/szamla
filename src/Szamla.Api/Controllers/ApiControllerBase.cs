using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Szamla.Api.Controllers;

[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentTenantId =>
        Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var tenantId)
            ? tenantId
            : throw new InvalidOperationException("A hitelesített felhasználó tokenjéből hiányzik a tenant_id igény.");
}
