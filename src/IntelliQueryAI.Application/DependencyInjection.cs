using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace IntelliQueryAI.Application;

/// <summary>
/// Configures dependency injection registrations for Application layer components.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the in-process CQRS Dispatcher, command handlers, query handlers, and FluentValidation validators.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        // Register all command and query handlers via Scrutor reflection scanning
        services.Scan(scan => scan
            .FromAssemblyOf<Dispatcher>()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)))
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
