using FluentAssertions;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Application.Features.Subscriptions.Commands.InitializePaystackPayment;
using IntelliQueryAI.Application.Features.Subscriptions.Commands.VerifyPaystackPayment;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.ValueObjects;
using Moq;

namespace IntelliQueryAI.Application.Tests;

public class PaystackPaymentCommandHandlerTests
{
    private readonly Mock<IWorkspaceStore> _workspaceStoreMock = new();
    private readonly Mock<ISubscriptionStore> _subscriptionStoreMock = new();
    private readonly Mock<IAuditLogStore> _auditLogStoreMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPaystackService> _paystackServiceMock = new();

    public PaystackPaymentCommandHandlerTests()
    {
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _paystackServiceMock
            .Setup(p => p.PublicKey)
            .Returns("pk_test_mock");
    }

    [Fact]
    public async Task Initialize_WhenWorkspaceExists_ReturnsSuccessWithAuthUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = Workspace.Create("Test Team", TenantSlug.Create("test-team").Value, SubscriptionPlan.Free, userId).Value;
        var workspaceId = workspace.Id;

        _workspaceStoreMock
            .Setup(w => w.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        _paystackServiceMock
            .Setup(p => p.InitializeTransactionAsync(It.IsAny<PaystackInitializeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaystackInitializeResult(
                Success: true,
                AuthorizationUrl: "https://checkout.paystack.com/access-123",
                AccessCode: "access-123",
                Reference: "IQ-SUB-ref-123",
                Message: "OK"));

        var handler = new InitializePaystackPaymentCommandHandler(
            _workspaceStoreMock.Object,
            _paystackServiceMock.Object);

        var command = new InitializePaystackPaymentCommand(
            workspaceId,
            userId,
            "test@example.com",
            SubscriptionPlan.Pro,
            "USD");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorizationUrl.Should().Be("https://checkout.paystack.com/access-123");
        result.Value.AccessCode.Should().Be("access-123");
        result.Value.Plan.Should().Be("Pro");
    }

    [Fact]
    public async Task Initialize_WhenWorkspaceNotFound_ReturnsFailure()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        _workspaceStoreMock
            .Setup(w => w.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var handler = new InitializePaystackPaymentCommandHandler(
            _workspaceStoreMock.Object,
            _paystackServiceMock.Object);

        var command = new InitializePaystackPaymentCommand(
            workspaceId,
            Guid.NewGuid(),
            "test@example.com",
            SubscriptionPlan.Pro,
            "USD");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PaymentErrors.WorkspaceNotFound.Code);
    }

    [Fact]
    public async Task Verify_WhenPaymentSuccessful_UpgradesSubscriptionAndWorkspaceToPro()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = Workspace.Create("Billing Workspace", TenantSlug.Create("billing-ws").Value, SubscriptionPlan.Free, userId).Value;
        var workspaceId = workspace.Id;
        var subscription = Subscription.CreateDefault(workspaceId, SubscriptionPlan.Free);
        string reference = $"IQ-SUB-{workspaceId:N}-123456";

        var meta = new Dictionary<string, string>
        {
            ["workspaceId"] = workspaceId.ToString(),
            ["userId"] = userId.ToString(),
            ["plan"] = "Pro"
        };

        _paystackServiceMock
            .Setup(p => p.VerifyTransactionAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaystackVerifyResult(
                Success: true,
                Status: "success",
                Reference: reference,
                Amount: 9900,
                Currency: "USD",
                GatewayResponse: "Successful",
                Metadata: meta,
                Message: "Approved"));

        _subscriptionStoreMock
            .Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _workspaceStoreMock
            .Setup(w => w.GetByIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var handler = new VerifyPaystackPaymentCommandHandler(
            _paystackServiceMock.Object,
            _subscriptionStoreMock.Object,
            _workspaceStoreMock.Object,
            _auditLogStoreMock.Object,
            _unitOfWorkMock.Object);

        var command = new VerifyPaystackPaymentCommand(reference);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Plan.Should().Be("Pro");
        result.Value.MaxDataSources.Should().Be(5);
        result.Value.MaxQueriesPerMonth.Should().Be(5000);

        subscription.Plan.Should().Be(SubscriptionPlan.Pro);
        workspace.SubscriptionPlan.Should().Be(SubscriptionPlan.Pro);

        _auditLogStoreMock.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.Action == "Subscription.Upgraded"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verify_WhenGatewayReportsFailure_ReturnsFailure()
    {
        // Arrange
        string reference = "IQ-SUB-failed-ref";
        _paystackServiceMock
            .Setup(p => p.VerifyTransactionAsync(reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaystackVerifyResult(
                Success: false,
                Status: "failed",
                Reference: reference,
                Amount: 0,
                Currency: "USD",
                GatewayResponse: "Declined",
                Metadata: new Dictionary<string, string>(),
                Message: "Card declined"));

        var handler = new VerifyPaystackPaymentCommandHandler(
            _paystackServiceMock.Object,
            _subscriptionStoreMock.Object,
            _workspaceStoreMock.Object,
            _auditLogStoreMock.Object,
            _unitOfWorkMock.Object);

        var command = new VerifyPaystackPaymentCommand(reference);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.VerificationFailed");
    }
}
