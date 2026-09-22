using Szamla.Application.Common.Cqrs;

namespace Szamla.Application.Tenants.Commands.RegisterTenant;

/// <summary>
/// Creates the tenant record itself. Creating the owner's login is a separate step (ASP.NET
/// Core Identity, orchestrated in the API layer within the same DB transaction) — Application
/// does not depend on Identity.
/// </summary>
public sealed record RegisterTenantCommand(
    string CompanyName,
    string TaxId,
    string Address,
    string DefaultCurrency = "HUF") : ICommand<Guid>;
