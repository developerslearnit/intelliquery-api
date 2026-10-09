using FluentAssertions;
using IntelliQueryAI.Application.Features.DataSources.Commands.RegisterDataSource;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class RegisterDataSourceCommandHandlerTests
{
    private readonly Mock<IDataSourceStore> _dataSourceStoreMock = new();
    private readonly Mock<ISubscriptionStore> _subscriptionStoreMock = new();
    private readonly Mock<IEncryptionService> _encryptionServiceMock = new();
    private readonly Mock<ISchemaDiscoveryEngine> _discoveryEngineMock = new();
    private readonly Mock<IAuditLogStore> _auditLogStoreMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RegisterDataSourceCommandHandler _handler;

    public RegisterDataSourceCommandHandlerTests()
    {
        _handler = new RegisterDataSourceCommandHandler(
            _dataSourceStoreMock.Object,
            _subscriptionStoreMock.Object,
            _encryptionServiceMock.Object,
            _discoveryEngineMock.Object,
            _auditLogStoreMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenValidAndUnderLimit_ShouldRegisterSuccessfully()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand(
            workspaceId,
            "Main Postgres DB",
            DatabaseType.PostgreSQL,
            "Host=db.prod;Database=main;Username=svc;",
            userId);

        var subscription = Subscription.CreateDefault(workspaceId, SubscriptionPlan.Pro);
        _subscriptionStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _dataSourceStoreMock.Setup(d => d.CountByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _discoveryEngineMock.Setup(e => e.TestConnectionAsync(command.DatabaseType, command.ConnectionString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _encryptionServiceMock.Setup(e => e.Encrypt(command.ConnectionString))
            .Returns("encrypted_secret_payload");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Main Postgres DB");
        result.Value.DatabaseType.Should().Be("PostgreSQL");
        result.Value.IsActive.Should().BeTrue();

        _dataSourceStoreMock.Verify(d => d.AddAsync(It.IsAny<DataSource>(), It.IsAny<CancellationToken>()), Times.Once);
        _auditLogStoreMock.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDataSourceLimitReached_ShouldReturnLimitError()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand(
            workspaceId,
            "Second DB",
            DatabaseType.SqlServer,
            "Server=db;Database=sql;",
            Guid.NewGuid());

        var subscription = Subscription.CreateDefault(workspaceId, SubscriptionPlan.Free); // MaxDataSources = 1
        _subscriptionStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _dataSourceStoreMock.Setup(d => d.CountByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Already at limit

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SubscriptionErrors.DataSourceLimitReached);
        _discoveryEngineMock.Verify(e => e.TestConnectionAsync(It.IsAny<DatabaseType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenConnectionFails_ShouldReturnConnectionFailedError()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand(
            workspaceId,
            "Unreachable DB",
            DatabaseType.MySql,
            "Server=invalid-host;Database=test;",
            Guid.NewGuid());

        _subscriptionStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        _discoveryEngineMock.Setup(e => e.TestConnectionAsync(command.DatabaseType, command.ConnectionString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DataSourceErrors.ConnectionFailed("").Code);
        _dataSourceStoreMock.Verify(d => d.AddAsync(It.IsAny<DataSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
