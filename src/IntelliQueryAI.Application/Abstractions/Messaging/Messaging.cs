using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace IntelliQueryAI.Application.Abstractions.Messaging;

/// <summary>
/// Marker interface representing a CQRS command that mutates state and returns a strongly typed response.
/// </summary>
/// <typeparam name="TResponse">The type of response returned upon handler execution.</typeparam>
public interface ICommand<TResponse>
{
}

/// <summary>
/// Marker interface representing a parameterless or non-generic CQRS command returning a standard <see cref="Result"/>.
/// </summary>
public interface ICommand : ICommand<Result>
{
}

/// <summary>
/// Defines a handler responsible for executing a specific command of type <typeparamref name="TCommand"/>.
/// </summary>
/// <typeparam name="TCommand">The command type being processed.</typeparam>
/// <typeparam name="TResponse">The expected response payload type.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Executes the business logic associated with the incoming command.
    /// </summary>
    /// <param name="command">The strongly typed command instance.</param>
    /// <param name="cancellationToken">Cancellation token to observe during asynchronous execution.</param>
    /// <returns>A task representing the asynchronous operation with the command outcome.</returns>
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a handler responsible for executing a non-generic command of type <typeparamref name="TCommand"/> returning a standard <see cref="Result"/>.
/// </summary>
/// <typeparam name="TCommand">The command type being processed.</typeparam>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Result>
    where TCommand : ICommand
{
}

/// <summary>
/// Marker interface representing a CQRS query that retrieves data without side effects.
/// </summary>
/// <typeparam name="TResponse">The query result payload type.</typeparam>
public interface IQuery<TResponse>
{
}

/// <summary>
/// Defines a handler responsible for executing a specific query of type <typeparamref name="TQuery"/>.
/// </summary>
/// <typeparam name="TQuery">The query type being evaluated.</typeparam>
/// <typeparam name="TResponse">The expected response type.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Executes the read-only query logic asynchronously.
    /// </summary>
    /// <param name="query">The strongly typed query instance.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>A task representing the query execution returning the result.</returns>
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-process mediator/dispatcher abstraction that routes commands and queries to their respective registered handlers.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Dispatches a command to its corresponding <see cref="ICommandHandler{TCommand, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="command">The command instance to execute.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>The response produced by the resolved command handler.</returns>
    Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a query to its corresponding <see cref="IQueryHandler{TQuery, TResponse}"/>.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="query">The query instance to execute.</param>
    /// <param name="cancellationToken">Cancellation token to observe.</param>
    /// <returns>The response produced by the resolved query handler.</returns>
    Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-process CQRS mediator that dynamically resolves and invokes command and query handlers via Dependency Injection.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dispatcher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The root or scoped service provider to resolve handlers from.</param>
    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Resolves the corresponding <see cref="ICommandHandler{TCommand, TResponse}"/> and executes it asynchronously.
    /// </summary>
    public async Task<TResponse> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResponse));
        var handler = _serviceProvider.GetRequiredService(handlerType);

        var method = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResponse>, TResponse>.HandleAsync))
            ?? throw new InvalidOperationException($"HandleAsync method not found on {handlerType.Name}");

        var task = (Task<TResponse>)method.Invoke(handler, [command, cancellationToken])!;
        return await task;
    }

    /// <summary>
    /// Resolves the corresponding <see cref="IQueryHandler{TQuery, TResponse}"/> and executes it asynchronously.
    /// </summary>
    public async Task<TResponse> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResponse));
        var handler = _serviceProvider.GetRequiredService(handlerType);

        var method = handlerType.GetMethod(nameof(IQueryHandler<IQuery<TResponse>, TResponse>.HandleAsync))
            ?? throw new InvalidOperationException($"HandleAsync method not found on {handlerType.Name}");

        var task = (Task<TResponse>)method.Invoke(handler, [query, cancellationToken])!;
        return await task;
    }
}
