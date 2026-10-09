using FluentAssertions;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.ValueObjects;

namespace IntelliQueryAI.Domain.Tests;

public class WorkspaceAggregateTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInitializeWorkspaceAndAssignOwner()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var slug = TenantSlug.Create("acme-analytics").Value;

        // Act
        var result = Workspace.Create("Acme Analytics", slug, SubscriptionPlan.Pro, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var workspace = result.Value;
        workspace.Name.Should().Be("Acme Analytics");
        workspace.Slug.Should().Be(slug);
        workspace.SubscriptionPlan.Should().Be(SubscriptionPlan.Pro);
        workspace.IsActive.Should().BeTrue();
        workspace.Members.Should().HaveCount(1);
        workspace.Members.First().UserId.Should().Be(ownerId);
        workspace.Members.First().Role.Should().Be(WorkspaceRole.Owner);
    }

    [Fact]
    public void AddMember_WhenUserNotMember_ShouldAddSuccessfully()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        var slug = TenantSlug.Create("acme-corp").Value;
        var workspace = Workspace.Create("Acme Corp", slug, SubscriptionPlan.Free, ownerId).Value;

        // Act
        var addResult = workspace.AddMember(newUserId, WorkspaceRole.Member);

        // Assert
        addResult.IsSuccess.Should().BeTrue();
        workspace.Members.Should().HaveCount(2);
        workspace.Members.Should().Contain(m => m.UserId == newUserId && m.Role == WorkspaceRole.Member);
    }

    [Fact]
    public void AddMember_WhenUserAlreadyMember_ShouldReturnConflictError()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var slug = TenantSlug.Create("acme-corp").Value;
        var workspace = Workspace.Create("Acme Corp", slug, SubscriptionPlan.Free, ownerId).Value;

        // Act (try adding owner again)
        var addResult = workspace.AddMember(ownerId, WorkspaceRole.Admin);

        // Assert
        addResult.IsFailure.Should().BeTrue();
        addResult.Error.Code.Should().Be(WorkspaceErrors.UserAlreadyMember.Code);
    }

    [Fact]
    public void RemoveMember_WhenOwner_ShouldReturnError()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var slug = TenantSlug.Create("acme-corp").Value;
        var workspace = Workspace.Create("Acme Corp", slug, SubscriptionPlan.Free, ownerId).Value;

        // Act
        var removeResult = workspace.RemoveMember(ownerId);

        // Assert
        removeResult.IsFailure.Should().BeTrue();
        removeResult.Error.Code.Should().Be(WorkspaceErrors.CannotRemoveOwner.Code);
    }
}
