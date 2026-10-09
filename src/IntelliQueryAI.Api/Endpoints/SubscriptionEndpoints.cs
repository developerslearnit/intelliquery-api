using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IntelliQueryAI.Api.Extensions;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Application.Features.Subscriptions.Commands.InitializePaystackPayment;
using IntelliQueryAI.Application.Features.Subscriptions.Commands.VerifyPaystackPayment;
using IntelliQueryAI.Application.Features.Subscriptions.Dtos;
using IntelliQueryAI.Application.Features.Subscriptions.Queries;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Infrastructure.Services.Paystack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IntelliQueryAI.Api.Endpoints;

public sealed record PaystackInitializeRequestDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SubscriptionPlan Plan = SubscriptionPlan.Pro,
    string Currency = "USD",
    string? CallbackUrl = null);

public sealed record PaystackVerifyRequestDto(
    string Reference);

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subscriptions").WithTags("Subscriptions");

        // 1. Get Subscription Details
        group.MapGet("/{workspaceId:guid}", async (
            [FromRoute] Guid workspaceId,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var query = new GetSubscriptionDetailsQuery(workspaceId);
            var result = await dispatcher.QueryAsync(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("GetSubscriptionDetails")
        .WithSummary("Retrieves current subscription tier, quota limits, and billing period for a workspace");

        // 2. Get Paystack Client Configuration
        group.MapGet("/paystack/config", (
            [FromServices] IOptions<PaystackOptions> options) =>
        {
            var config = new PaystackConfigDto(
                options.Value.PublicKey,
                options.Value.Currency);

            return Results.Ok(config);
        })
        .RequireAuthorization()
        .WithName("GetPaystackConfig")
        .WithSummary("Retrieves Paystack public key and default currency for client inline checkout");

        // 3. Initialize Paystack Payment
        group.MapPost("/{workspaceId:guid}/paystack/initialize", async (
            [FromRoute] Guid workspaceId,
            [FromBody] PaystackInitializeRequestDto request,
            ClaimsPrincipal user,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            string? email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
            if (string.IsNullOrWhiteSpace(email))
            {
                email = "billing@intelliquery.ai";
            }

            var command = new InitializePaystackPaymentCommand(
                workspaceId,
                userId,
                email,
                request.Plan,
                request.Currency,
                request.CallbackUrl);

            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("InitializePaystackPayment")
        .WithSummary("Initializes a Paystack checkout transaction for upgrading workspace subscription");

        // 4. Verify Paystack Payment
        group.MapPost("/paystack/verify", async (
            [FromBody] PaystackVerifyRequestDto request,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            var command = new VerifyPaystackPaymentCommand(request.Reference);
            var result = await dispatcher.SendAsync(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization()
        .WithName("VerifyPaystackPayment")
        .WithSummary("Verifies a completed Paystack transaction reference and upgrades the workspace tier");

        // 5. Paystack Webhook Handler (Public endpoint, signature verified)
        group.MapPost("/paystack/webhook", async (
            HttpRequest httpRequest,
            [FromServices] IPaystackService paystackService,
            [FromServices] IDispatcher dispatcher,
            CancellationToken ct) =>
        {
            string signature = httpRequest.Headers["x-paystack-signature"].ToString();
            using var reader = new StreamReader(httpRequest.Body);
            string requestBody = await reader.ReadToEndAsync(ct);

            if (!paystackService.VerifyWebhookSignature(requestBody, signature))
            {
                return Results.Unauthorized();
            }

            try
            {
                var jsonNode = JsonNode.Parse(requestBody);
                string? eventType = jsonNode?["event"]?.GetValue<string>();

                if (string.Equals(eventType, "charge.success", StringComparison.OrdinalIgnoreCase))
                {
                    string? reference = jsonNode?["data"]?["reference"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(reference))
                    {
                        var command = new VerifyPaystackPaymentCommand(reference);
                        await dispatcher.SendAsync(command, ct);
                    }
                }

                return Results.Ok();
            }
            catch
            {
                return Results.BadRequest();
            }
        })
        .AllowAnonymous()
        .WithName("PaystackWebhook")
        .WithSummary("Processes verified asynchronous payment webhook notifications from Paystack");

        return app;
    }
}
