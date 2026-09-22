using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Szamla.Application.Common.Cqrs;

/// <summary>
/// Minimal in-process command/query dispatcher. Resolves the matching
/// ICommandHandler{,}/IQueryHandler{,} from DI and invokes it. Deliberately hand-rolled instead
/// of pulling in MediatR, to avoid that package's commercial licensing terms for a mechanism
/// this small.
/// </summary>
internal sealed class Dispatcher : ISender
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        => Invoke<TResponse>(typeof(ICommandHandler<,>), command, cancellationToken);

    public Task<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        => Invoke<TResponse>(typeof(IQueryHandler<,>), query, cancellationToken);

    private Task<TResponse> Invoke<TResponse>(Type openHandlerType, object request, CancellationToken cancellationToken)
    {
        var handlerType = openHandlerType.MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = _serviceProvider.GetRequiredService(handlerType);
        var method = handlerType.GetMethod("Handle", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"A(z) {handlerType.Name} nem tartalmaz Handle metódust.");

        return (Task<TResponse>)method.Invoke(handler, [request, cancellationToken])!;
    }
}
