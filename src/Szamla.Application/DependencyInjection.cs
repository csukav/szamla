using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Szamla.Application.Common.Cqrs;

namespace Szamla.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISender, Dispatcher>();

        var openHandlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };
        var assembly = typeof(DependencyInjection).Assembly;

        var implementationTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false });

        foreach (var implementationType in implementationTypes)
        {
            var matchingInterfaces = implementationType.GetInterfaces()
                .Where(i => i.IsGenericType && openHandlerInterfaces.Contains(i.GetGenericTypeDefinition()));

            foreach (var handlerInterface in matchingInterfaces)
            {
                services.AddScoped(handlerInterface, implementationType);
            }
        }

        return services;
    }
}
