using FluentAssertions;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using Xunit;

namespace IntelliQueryAI.Domain.Tests;

public sealed class DataSourceAggregateTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var name = "Production Postgres";
        var databaseType = DatabaseType.PostgreSQL;
        var encryptedCs = "enc:v1:testnonce:testcipher:testtag";

        // Act
        var result = DataSource.Create(workspaceId, name, databaseType, encryptedCs);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.WorkspaceId.Should().Be(workspaceId);
        result.Value.Name.Should().Be(name);
        result.Value.DatabaseType.Should().Be(databaseType);
        result.Value.EncryptedConnectionString.Should().Be(encryptedCs);
        result.Value.IsActive.Should().BeTrue();
        result.Value.SyncStatus.Should().Be(SyncStatus.Pending);
        result.Value.LastSyncAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyName_ShouldReturnFailure()
    {
        // Arrange & Act
        var result = DataSource.Create(Guid.NewGuid(), "   ", DatabaseType.SqlServer, "valid_cipher");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.NameRequired);
    }

    [Fact]
    public void Create_WithEmptyConnectionString_ShouldReturnFailure()
    {
        // Arrange & Act
        var result = DataSource.Create(Guid.NewGuid(), "Test DB", DatabaseType.MySql, "");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DataSourceErrors.ConnectionStringRequired);
    }

    [Fact]
    public void EnableAndDisable_ShouldToggleIsActiveState()
    {
        // Arrange
        var ds = DataSource.Create(Guid.NewGuid(), "Test DB", DatabaseType.PostgreSQL, "ciphertext").Value;
        ds.IsActive.Should().BeTrue();

        // Act & Assert Disable
        ds.Disable();
        ds.IsActive.Should().BeFalse();

        // Act & Assert Enable
        ds.Enable();
        ds.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SyncLifecycle_ShouldTransitionStatusesCorrectly()
    {
        // Arrange
        var ds = DataSource.Create(Guid.NewGuid(), "Test DB", DatabaseType.PostgreSQL, "ciphertext").Value;
        ds.SyncStatus.Should().Be(SyncStatus.Pending);

        // Act: MarkSyncStarted
        ds.MarkSyncStarted();
        ds.SyncStatus.Should().Be(SyncStatus.InProgress);
        ds.SyncErrorMessage.Should().BeNull();

        // Act: MarkSyncCompleted
        ds.MarkSyncCompleted();
        ds.SyncStatus.Should().Be(SyncStatus.Completed);
        ds.LastSyncAt.Should().NotBeNull();
        ds.SyncErrorMessage.Should().BeNull();

        // Act: MarkSyncFailed
        ds.MarkSyncFailed("Connection timed out after 30 seconds");
        ds.SyncStatus.Should().Be(SyncStatus.Failed);
        ds.SyncErrorMessage.Should().Be("Connection timed out after 30 seconds");
    }
}
