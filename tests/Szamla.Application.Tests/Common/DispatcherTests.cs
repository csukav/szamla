using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Szamla.Application;
using Szamla.Application.Common.Cqrs;
using Xunit;

namespace Szamla.Application.Tests.Common;

public sealed record PingCommand(string Message) : ICommand<string>;

public sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    public Task<string> Handle(PingCommand command, CancellationToken cancellationToken)
        => Task.FromResult($"pong:{command.Message}");
}

public sealed record PingQuery(int Value) : IQuery<int>;

public sealed class PingQueryHandler : IQueryHandler<PingQuery, int>
{
    public Task<int> Handle(PingQuery query, CancellationToken cancellationToken)
        => Task.FromResult(query.Value * 2);
}

public class DispatcherTests
{
    private static ISender BuildSender()
    {
        // AddApplication() only scans Szamla.Application's own assembly for handlers, so the
        // Ping* handlers declared here (in the test assembly) are wired up explicitly. This test
        // exercises the dispatcher's resolve-and-invoke mechanics, not the assembly scan.
        var services = new ServiceCollection()
            .AddApplication()
            .AddScoped<ICommandHandler<PingCommand, string>, PingCommandHandler>()
            .AddScoped<IQueryHandler<PingQuery, int>, PingQueryHandler>();

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Send_Command_ResolvesRegisteredHandlerAndReturnsItsResult()
    {
        var sender = BuildSender();

        var result = await sender.Send(new PingCommand("hello"));

        result.Should().Be("pong:hello");
    }

    [Fact]
    public async Task Send_Query_ResolvesRegisteredHandlerAndReturnsItsResult()
    {
        var sender = BuildSender();

        var result = await sender.Send(new PingQuery(21));

        result.Should().Be(42);
    }
}
