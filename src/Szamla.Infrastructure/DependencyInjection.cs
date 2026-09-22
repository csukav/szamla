using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Szamla.Application.Common.Interfaces;
using Szamla.Infrastructure.Auditing;
using Szamla.Infrastructure.Identity;
using Szamla.Infrastructure.Invoicing;
using Szamla.Infrastructure.Multitenancy;
using Szamla.Infrastructure.Pdf;
using Szamla.Infrastructure.Persistence;

namespace Szamla.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Community license: confirmed with the client that revenue is under QuestPDF's
        // threshold (see README's licensing section) — re-check before relying on this further,
        // especially across a major QuestPDF version bump.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // The connection string is read lazily, inside the options callback, rather than once
        // up front: WebApplicationFactory-based tests append their own configuration (e.g. a
        // Testcontainers connection string) via ConfigureAppConfiguration, but that only takes
        // effect after this method returns — an eager read here would permanently capture the
        // pre-override value.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) => options
            .UseNpgsql(configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("A 'ConnectionStrings:DefaultConnection' konfigurációs érték hiányzik."))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<IJwtTokenGenerator>(sp => sp.GetRequiredService<JwtTokenService>());
        services.AddScoped<RefreshTokenIssuer>();
        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        services.AddSingleton<IInvoicePdfGenerator, InvoicePdfGenerator>();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false; // TODO: bekapcsolni, ha az e-mail megerősítési folyamat elkészül
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddSignInManager();

        return services;
    }
}
