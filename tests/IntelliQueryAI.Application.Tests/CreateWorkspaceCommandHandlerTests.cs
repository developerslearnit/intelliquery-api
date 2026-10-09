using FluentAssertions;
using IntelliQueryAI.Application.Features.Workspaces.Commands.CreateWorkspace;
using IntelliQueryAI.Application.Features.Workspaces.Queries;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.ValueObjects;
using Moq;

namespace IntelliQueryAI.Application.Tests;

public class CreateWorkspaceCommandHandlerTests
{
    private readonly Mock<IWorkspaceStore> _workspaceStoreMock = new();
    private readonly Mock<ISubscriptionStore> _subscriptionStoreMock = new();
    private readonly Mock<IAuditLogStore> _auditLogStoreMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly CreateWorkspaceCommandHandler _handler;

    public CreateWorkspaceCommandHandlerTests()
    {
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new CreateWorkspaceCommandHandler(
            _workspaceStoreMock.Object,
            _subscriptionStoreMock.Object,
            _auditLogStoreMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenFreePlanUserAlreadyHasWorkspace_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingWorkspace = Workspace.Create("Acme 1", TenantSlug.Create("acme-1").Value, SubscriptionPlan.Free, userId).Value;

        _workspaceStoreMock
            .Setup(w => w.SlugExistsAsync("acme-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _workspaceStoreMock
            .Setup(w => w.GetUserWorkspacesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Workspace> { existingWorkspace });

        var command = new CreateWorkspaceCommand(userId, "Acme 2", "acme-2", SubscriptionPlan.Free);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.FreePlanWorkspaceLimitReached.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenProPlanUserCreatesAdditionalWorkspace_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var proWorkspace = Workspace.Create("Acme Pro", TenantSlug.Create("acme-pro").Value, SubscriptionPlan.Pro, userId).Value;

        _workspaceStoreMock
            .Setup(w => w.SlugExistsAsync("acme-new", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _workspaceStoreMock
            .Setup(w => w.GetUserWorkspacesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Workspace> { proWorkspace });

        var command = new CreateWorkspaceCommand(userId, "Acme New", "acme-new", SubscriptionPlan.Free);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Acme New");
    }

    [Fact]
    public async Task GetUserWorkspaces_WhenFreeTierUser_ReturnsOnlyOneWorkspace()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ws1 = Workspace.Create("Primary Workspace", TenantSlug.Create("primary-ws").Value, SubscriptionPlan.Free, userId).Value;
        var ws2 = Workspace.Create("Second Workspace", TenantSlug.Create("second-ws").Value, SubscriptionPlan.Free, userId).Value;

        var queryHandler = new GetUserWorkspacesQueryHandler(_workspaceStoreMock.Object);

        _workspaceStoreMock
            .Setup(w => w.GetUserWorkspacesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Workspace> { ws1, ws2 });

        var query = new GetUserWorkspacesQuery(userId);

        // Act
        var result = await queryHandler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Primary Workspace");
    }
}
