using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Szamla.Infrastructure.Multitenancy;

namespace Szamla.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add/update` construct an ApplicationDbContext outside of the
/// API host (no HTTP request, so no tenant to resolve — NullCurrentTenantService stands in).
/// The connection string is read from SZAMLA_CONNECTION_STRING, falling back to a local dev
/// default; never a value we'd want committed for a real environment.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string DefaultLocalConnectionString =
        "Host=localhost;Port=5432;Database=szamla;Username=szamla;Password=szamla_dev_only";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SZAMLA_CONNECTION_STRING")
            ?? DefaultLocalConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options, new NullCurrentTenantService());
    }
}
