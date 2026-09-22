using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Szamla.Api.Tests;

/// <summary>
/// Boots the real ASP.NET Core host and exercises only the liveness endpoint, which by design
/// touches no database — so this test (unlike the Testcontainers-backed ones in this project)
/// runs on a machine with no PostgreSQL/Docker available.
/// </summary>
public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Startup:SeedRolesOnStartup"] = "false",
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused-in-this-test;Username=x;Password=x",
                    ["Jwt:Issuer"] = "Szamla.Tests",
                    ["Jwt:Audience"] = "Szamla.Tests.Client",
                    ["Jwt:SigningKey"] = Convert.ToBase64String(new byte[32]),
                });
            });
        });
    }

    [Fact]
    public async Task GetHealthLive_ReturnsOkWithoutTouchingTheDatabase()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
