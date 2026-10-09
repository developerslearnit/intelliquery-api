using FluentAssertions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace IntelliQueryAI.Application.Tests;

public record PingCommand(string Message) : ICommand<Result<string>>;

public class PingCommandHandler : ICommandHandler<PingCommand, Result<string>>
{
    public Task<Result<string>> HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success($"Pong: {command.Message}"));
    }
}

public class DispatcherTests
{
    [Fact]
    public async Task SendAsync_ShouldResolveAndExecuteHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<PingCommand, Result<string>>, PingCommandHandler>();
        services.AddScoped<IDispatcher, Dispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        // Act
        var result = await dispatcher.SendAsync(new PingCommand("Hello Antigravity"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Pong: Hello Antigravity");
    }
}
