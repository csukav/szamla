using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Szamla.Api.Contracts.Auth;
using Szamla.Application.Common.Constants;
using Szamla.Application.Common.Cqrs;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Tenants.Commands.RegisterTenant;
using Szamla.Infrastructure.Identity;
using Szamla.Infrastructure.Persistence;

namespace Szamla.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ISender sender,
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    JwtTokenService jwtTokenService,
    RefreshTokenIssuer refreshTokenIssuer) : ControllerBase
{
    [HttpPost("register-tenant")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterTenantResponse>> RegisterTenant(
        RegisterTenantRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Guid tenantId;
        try
        {
            tenantId = await sender.Send(
                new RegisterTenantCommand(request.CompanyName, request.TaxId, request.Address, request.DefaultCurrency),
                cancellationToken);
        }
        catch (ConflictException ex)
        {
            return Conflict(new ProblemDetails { Title = ex.Message, Status = StatusCodes.Status409Conflict });
        }

        var owner = new ApplicationUser
        {
            TenantId = tenantId,
            UserName = request.OwnerEmail,
            Email = request.OwnerEmail,
            FullName = request.OwnerFullName,
        };

        var createResult = await userManager.CreateAsync(owner, request.OwnerPassword);
        if (!createResult.Succeeded)
        {
            return ValidationProblem(BuildIdentityErrorModelState(createResult));
        }

        await userManager.AddToRoleAsync(owner, Roles.Owner);

        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(nameof(RegisterTenant), new RegisterTenantResponse(tenantId, owner.Id));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new ProblemDetails { Title = "Hibás e-mail cím vagy jelszó." });
        }

        if (await userManager.GetTwoFactorEnabledAsync(user))
        {
            var pendingToken = jwtTokenService.GeneratePendingTwoFactorToken(user.Id);
            return Ok(new LoginResponse(true, pendingToken, null, null, null));
        }

        return Ok(await IssueTokenPairAsync(user, cancellationToken));
    }

    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> VerifyTwoFactor(VerifyTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var userId = jwtTokenService.ValidatePendingTwoFactorToken(request.TwoFactorToken);
        if (userId is null)
        {
            return Unauthorized(new ProblemDetails { Title = "Lejárt vagy érvénytelen kétfaktoros munkamenet, jelentkezz be újra." });
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var isValidCode = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, request.Code);
        if (!isValidCode)
        {
            return Unauthorized(new ProblemDetails { Title = "Érvénytelen hitelesítő kód." });
        }

        return Ok(await IssueTokenPairAsync(user, cancellationToken));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = await refreshTokenIssuer.ConsumeAsync(request.RefreshToken, cancellationToken);
        if (userId is null)
        {
            return Unauthorized(new ProblemDetails { Title = "Érvénytelen vagy lejárt frissítő token." });
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var loginResponse = await IssueTokenPairAsync(user, cancellationToken);
        return Ok(new TokenResponse(loginResponse.AccessToken!, loginResponse.RefreshToken!, loginResponse.AccessTokenExpiresAtUtc!.Value));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await refreshTokenIssuer.ConsumeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<ActionResult<EnableTwoFactorResponse>> EnableTwoFactor()
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("A felhasználó nem található.");

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var otpAuthUri = $"otpauth://totp/Szamla:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer=Szamla&digits=6";

        return Ok(new EnableTwoFactorResponse(key!, otpAuthUri));
    }

    [HttpPost("2fa/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmTwoFactor(ConfirmTwoFactorRequest request)
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("A felhasználó nem található.");

        var isValidCode = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, request.Code);
        if (!isValidCode)
        {
            return BadRequest(new ProblemDetails { Title = "Érvénytelen hitelesítő kód." });
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        return NoContent();
    }

    private async Task<LoginResponse> IssueTokenPairAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email!, user.TenantId, roles);
        var accessTokenExpiresAtUtc = jwtTokenService.GetAccessTokenExpiry();
        var (refreshToken, _) = await refreshTokenIssuer.IssueAsync(user.Id, cancellationToken);

        return new LoginResponse(false, null, accessToken, refreshToken, accessTokenExpiresAtUtc);
    }

    private static IDictionary<string, string[]> BuildIdentityErrorModelState(IdentityResult result)
        => result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

    private ActionResult ValidationProblem(IDictionary<string, string[]> errors)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var (key, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(key, message);
            }
        }

        return ValidationProblem(modelState);
    }
}
