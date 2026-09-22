namespace Szamla.Application.Common.Cqrs;

/// <summary>A side-effect-free request that reads data and yields a result of type <typeparamref name="TResponse"/>.</summary>
public interface IQuery<TResponse>
{
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
