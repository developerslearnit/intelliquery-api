using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Identity.Commands.ForgotPassword;
using IntelliQueryAI.Application.Features.Identity.Commands.LoginUser;
using IntelliQueryAI.Application.Features.Identity.Commands.RefreshToken;
using IntelliQueryAI.Application.Features.Identity.Commands.RegisterUser;
using IntelliQueryAI.Application.Features.Identity.Commands.ResetPassword;
using IntelliQueryAI.Application.Features.Identity.Commands.VerifyEmail;
using IntelliQueryAI.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace IntelliQueryAI.Api.Endpoints;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? WorkspaceName = null,
    string? WorkspaceSlug = null);

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", async (
            [FromBody] RegisterUserRequest request,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            string effectiveName = !string.IsNullOrWhiteSpace(request.WorkspaceName)
                ? request.WorkspaceName.Trim()
                : $"{request.FirstName}'s Workspace";

            string effectiveSlug = !string.IsNullOrWhiteSpace(request.WorkspaceSlug)
                ? request.WorkspaceSlug.Trim()
                : TenantSlug.Generate(effectiveName);

            var command = new RegisterUserCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                effectiveName,
                effectiveSlug);

            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .WithName("RegisterUser")
        .WithSummary("Registers a new user and initial tenant workspace");

        group.MapPost("/login", async (
            [FromBody] LoginUserCommand command,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .WithName("LoginUser")
        .WithSummary("Authenticates user credentials and returns JWT access & refresh tokens");

        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenCommand command,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .WithName("RefreshToken")
        .WithSummary("Rotates refresh token and generates fresh access token");

        group.MapPost("/verify-email", async (
            [FromBody] VerifyEmailCommand command,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyEmail")
        .WithSummary("Verifies email address using a one-time verification token");

        group.MapPost("/forgot-password", async (
            [FromBody] ForgotPasswordCommand command,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .WithName("ForgotPassword")
        .WithSummary("Generates password reset token and sends recovery email");

        group.MapPost("/reset-password", async (
            [FromBody] ResetPasswordCommand command,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .WithName("ResetPassword")
        .WithSummary("Resets password using a validated password reset token");

        return app;
    }
}
