using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntelliQueryAI.IntegrationTests;

public class SubscriptionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SubscriptionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InitializePaystack_WithStringPlan_ShouldSucceedAndReturnAuthorizationUrl()
    {
        // Arrange
        var testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IPaystackService, TestPaystackService>();
            });
        });

        using var scope = testFactory.Services.CreateScope();
        var wsStore = scope.ServiceProvider.GetRequiredService<IWorkspaceStore>();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var tokenGen = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var user = User.Create(EmailAddress.Create($"sub_test_{Guid.NewGuid():N}@example.com").Value, "Test", "User", "Hash123!").Value;
        await userStore.AddAsync(user);

        var ws = Workspace.Create("Test Paystack Workspace", TenantSlug.Create($"paystack-ws-{Guid.NewGuid():N}").Value, SubscriptionPlan.Free, user.Id).Value;
        await wsStore.AddAsync(ws);

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.SaveChangesAsync();

        var token = tokenGen.GenerateAccessToken(user, ws, "Owner");

        var client = testFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", ws.Id.ToString());

        var payload = new
        {
            plan = "Pro",
            currency = "USD",
            callbackUrl = "http://localhost:3000/admin"
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/subscriptions/{ws.Id}/paystack/initialize", payload);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Expected 200 OK but got {response.StatusCode} with body: {content}");
        content.Should().Contain("https://checkout.paystack.com/test-auth-url");
        content.Should().Contain("pk_test_1680c08b7b51fb92fec7eab72d30249125e915ec");
        content.Should().Contain("Pro");
    }

    private sealed class TestPaystackService : IPaystackService
    {
        public string PublicKey => "pk_test_1680c08b7b51fb92fec7eab72d30249125e915ec";

        public Task<PaystackInitializeResult> InitializeTransactionAsync(
            PaystackInitializeRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PaystackInitializeResult(
                Success: true,
                AuthorizationUrl: "https://checkout.paystack.com/test-auth-url",
                AccessCode: "test_access_code",
                Reference: request.Reference,
                Message: "Authorization URL created"));
        }

        public Task<PaystackVerifyResult> VerifyTransactionAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PaystackVerifyResult(
                Success: true,
                Status: "success",
                Reference: reference,
                Amount: 9900,
                Currency: "USD",
                GatewayResponse: "Successful",
                Metadata: new Dictionary<string, string>(),
                Message: "Verified"));
        }

        public bool VerifyWebhookSignature(string requestBody, string signature) => true;
    }

    [Fact]
    public async Task GetSubscription_WhenWorkspaceExistsWithoutSubscription_AutoHealsAndReturnsOk()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var wsStore = scope.ServiceProvider.GetRequiredService<IWorkspaceStore>();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore>();
        var tokenGen = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var user = User.Create(EmailAddress.Create($"sub_test_{Guid.NewGuid():N}@example.com").Value, "Test", "User", "Hash123!").Value;
        await userStore.AddAsync(user);

        var ws = Workspace.Create("Auto Heal Workspace", TenantSlug.Create($"auto-heal-{Guid.NewGuid():N}").Value, SubscriptionPlan.Free, user.Id).Value;
        await wsStore.AddAsync(ws);

        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.SaveChangesAsync();

        var token = tokenGen.GenerateAccessToken(user, ws, "Owner");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", ws.Id.ToString());

        // Act: Request subscription details for workspace with NO prior subscription row
        var response = await client.GetAsync($"/api/subscriptions/{ws.Id}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert: Should NOT return 404, but 200 OK with default Free plan
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Expected 200 OK after self-healing but got {response.StatusCode}: {content}");
        content.Should().Contain("Free");
    }
}
