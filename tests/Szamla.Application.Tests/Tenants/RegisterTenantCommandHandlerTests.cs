using FluentAssertions;
using Szamla.Application.Common.Exceptions;
using Szamla.Application.Tenants.Commands.RegisterTenant;
using Szamla.Application.Tests.Common;
using Xunit;

namespace Szamla.Application.Tests.Tenants;

public class RegisterTenantCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithNewTaxId_CreatesTenantAndPersistsIt()
    {
        await using var dbContext = FakeApplicationDbContext.Create();
        var handler = new RegisterTenantCommandHandler(dbContext);
        var command = new RegisterTenantCommand("Teszt Kft.", "12345678-1-42", "1011 Budapest, Fő utca 1.");

        var tenantId = await handler.Handle(command, CancellationToken.None);

        tenantId.Should().NotBeEmpty();
        dbContext.Tenants.Should().ContainSingle(t => t.Id == tenantId && t.TaxId == "12345678-1-42");
    }

    [Fact]
    public async Task Handle_WithAlreadyRegisteredTaxId_ThrowsConflictException()
    {
        await using var dbContext = FakeApplicationDbContext.Create();
        var handler = new RegisterTenantCommandHandler(dbContext);
        var command = new RegisterTenantCommand("Teszt Kft.", "12345678-1-42", "cím");
        await handler.Handle(command, CancellationToken.None);

        var duplicate = new RegisterTenantCommand("Másik Kft.", "12345678-1-42", "másik cím");
        var act = () => handler.Handle(duplicate, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
