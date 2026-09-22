using Microsoft.AspNetCore.Identity;
using Szamla.Application.Common.Constants;

namespace Szamla.Infrastructure.Identity;

/// <summary>Ensures the four fixed MVP roles exist. Called once at API startup.</summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }
    }
}
