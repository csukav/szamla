using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Szamla.Api.Contracts.Auth;
using Szamla.Infrastructure.Multitenancy;
using Szamla.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Szamla.Api.Tests;

/// <summary>
/// End-to-end test of register-tenant -> login against a real PostgreSQL instance. Requires
/// Docker (Testcontainers spins up the database) — this is the CI-only counterpart to
/// HealthCheckTests, which deliberately avoids that dependency.
/// </summary>
public class AuthFlowTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("szamla_test")
        .WithUsername("szamla_test")
        .WithPassword("szamla_test")
        .Build();

    private WebApplicationFactory<Program> _factory = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Migrate through a standalone DbContext, before the factory's host is ever started.
        // Accessing WebApplicationFactory.Services (below) boots the real Program.cs, which
        // seeds roles at startup — against an unmigrated database that would still fail.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using (var migrationContext = new ApplicationDbContext(options, new NullCurrentTenantService()))
        {
            await migrationContext.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                    ["Jwt:Issuer"] = "Szamla.Tests",
                    ["Jwt:Audience"] = "Szamla.Tests.Client",
                    ["Jwt:SigningKey"] = Convert.ToBase64String(new byte[32]),
                });
            });
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task RegisterTenant_ThenLogin_IssuesAccessAndRefreshTokens()
    {
        var client = _factory.CreateClient();
        var request = new RegisterTenantRequest(
            CompanyName: "E2E Teszt Kft.",
            TaxId: "11122233-1-42",
            Address: "1011 Budapest, Teszt utca 1.",
            OwnerFullName: "Teszt Elek",
            OwnerEmail: "teszt.elek@example.com",
            OwnerPassword: "NagyonJelszo123");

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register-tenant", request);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(request.OwnerEmail, request.OwnerPassword));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.RequiresTwoFactor.Should().BeFalse();
        login.AccessToken.Should().NotBeNullOrWhiteSpace();
        login.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterTenant_WithAlreadyUsedTaxId_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var first = new RegisterTenantRequest(
            "Első Kft.", "44455566-1-42", "cím", "Teszt Elek", "elso@example.com", "NagyonJelszo123");
        var second = first with { CompanyName = "Második Kft.", OwnerEmail = "masodik@example.com" };

        (await client.PostAsJsonAsync("/api/auth/register-tenant", first)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        var conflictResponse = await client.PostAsJsonAsync("/api/auth/register-tenant", second);

        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new RegisterTenantRequest(
            "Harmadik Kft.", "77788899-1-42", "cím", "Teszt Elek", "harmadik@example.com", "NagyonJelszo123");
        await client.PostAsJsonAsync("/api/auth/register-tenant", request);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(request.OwnerEmail, "RosszJelszo"));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
