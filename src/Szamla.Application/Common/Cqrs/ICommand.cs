namespace Szamla.Application.Common.Cqrs;

/// <summary>A request that changes state and yields a result of type <typeparamref name="TResponse"/>.</summary>
public interface ICommand<TResponse>
{
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}
