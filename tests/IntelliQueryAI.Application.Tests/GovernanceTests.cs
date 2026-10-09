using System.Text.Json;
using FluentAssertions;
using IntelliQueryAI.Application.Features.Governance.Commands;
using IntelliQueryAI.Application.Features.Governance.Dtos;
using IntelliQueryAI.Application.Features.Governance.Queries;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Governance;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class GovernanceTests
{
    private readonly Mock<IGovernancePolicyStore> _policyStoreMock = new();
    private readonly Mock<IAuditLogStore> _auditStoreMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly IDataMaskingEngine _maskingEngine = new DataMaskingEngine();

    [Theory]
    [InlineData(MaskingStrategy.Full, "4111222233334444", "********")]
    [InlineData(MaskingStrategy.Partial, "4111222233334444", "************4444")]
    [InlineData(MaskingStrategy.Email, "alice.smith@corp.com", "a***@corp.com")]
    [InlineData(MaskingStrategy.Nullify, "TopSecretPassword", "[RESTRICTED]")]
    [InlineData(MaskingStrategy.None, "PublicInfo", "PublicInfo")]
    public void MaskValue_AppliesStrategyCorrectly(MaskingStrategy strategy, string input, string expected)
    {
        // Act
        string result = _maskingEngine.MaskValue(input, strategy);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MaskValue_Hash_ReturnsDeterministicPrefixedHex()
    {
        // Act
        string hash1 = _maskingEngine.MaskValue("secret_123", MaskingStrategy.Hash);
        string hash2 = _maskingEngine.MaskValue("secret_123", MaskingStrategy.Hash);

        // Assert
        hash1.Should().StartWith("[HASH:");
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void MaskRow_WhenAiContextAndAiMaskingEnforced_AlwaysMasksEvenForAdmin()
    {
        // Arrange
        var policy = GovernancePolicy.Create(
            workspaceId: Guid.NewGuid(),
            name: "Protect SSN",
            description: "PII masking for SSN",
            targetTable: "customers",
            targetColumn: "social_security",
            classification: DataClassification.Pii,
            maskingStrategy: MaskingStrategy.Partial,
            allowedRolesJson: JsonSerializer.Serialize(new[] { "Admin", "ComplianceOfficer" }),
            isAiMaskingEnforced: true);

        var policies = new[] { policy };
        var row = new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["name"] = "Alice",
            ["social_security"] = "123-45-6789"
        };

        // Act - AI Context with Admin role
        var maskedAiRow = _maskingEngine.MaskRow(row, "customers", policies, userRole: "Admin", isAiContext: true);

        // Assert - Critical Zero AI Data Leakage rule: Must be masked before reaching LLM
        maskedAiRow["social_security"].Should().Be("*******6789");
    }

    [Fact]
    public void MaskRow_WhenUserContext_AllowsAccessIfRoleAuthorized()
    {
        // Arrange
        var policy = GovernancePolicy.Create(
            workspaceId: Guid.NewGuid(),
            name: "Protect SSN",
            description: "PII masking for SSN",
            targetTable: "customers",
            targetColumn: "social_security",
            classification: DataClassification.Pii,
            maskingStrategy: MaskingStrategy.Partial,
            allowedRolesJson: JsonSerializer.Serialize(new[] { "Admin", "ComplianceOfficer" }),
            isAiMaskingEnforced: true);

        var policies = new[] { policy };
        var row = new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["social_security"] = "123-45-6789"
        };

        // Act 1: Admin user (allowed)
        var adminRow = _maskingEngine.MaskRow(row, "customers", policies, userRole: "Admin", isAiContext: false);

        // Act 2: Standard Member user (unauthorized)
        var memberRow = _maskingEngine.MaskRow(row, "customers", policies, userRole: "Member", isAiContext: false);

        // Assert
        adminRow["social_security"].Should().Be("123-45-6789");
        memberRow["social_security"].Should().Be("*******6789");
    }

    [Fact]
    public async Task CreateGovernancePolicy_WhenValid_PersistsAndAudits()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var command = new CreateGovernancePolicyCommand(
            WorkspaceId: workspaceId,
            UserId: userId,
            Name: "Mask Credit Cards",
            Description: "PCI-DSS masking for payment cards",
            TargetTable: "payments",
            TargetColumn: "card_number",
            Classification: "Financial",
            MaskingStrategy: "Partial",
            AllowedRoles: new[] { "Admin" },
            IsActive: true,
            IsAiMaskingEnforced: true);

        var handler = new CreateGovernancePolicyCommandHandler(
            _policyStoreMock.Object,
            _auditStoreMock.Object,
            _tenantContextMock.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Mask Credit Cards");
        result.Value.TargetColumn.Should().Be("card_number");
        result.Value.Classification.Should().Be("Financial");
        result.Value.MaskingStrategy.Should().Be("Partial");

        _policyStoreMock.Verify(s => s.AddAsync(It.Is<GovernancePolicy>(p =>
            p.WorkspaceId == workspaceId &&
            p.Name == "Mask Credit Cards" &&
            p.TargetColumn == "card_number"), It.IsAny<CancellationToken>()), Times.Once);

        _auditStoreMock.Verify(a => a.AddAsync(It.Is<AuditLog>(l =>
            l.WorkspaceId == workspaceId &&
            l.Action == "Governance.PolicyCreated"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGovernancePolicies_WhenTenantMatches_ReturnsPolicyList()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var policy = GovernancePolicy.Create(
            workspaceId: workspaceId,
            name: "Protect Emails",
            description: "Mask email addresses",
            targetTable: "users",
            targetColumn: "email",
            classification: DataClassification.Pii,
            maskingStrategy: MaskingStrategy.Email);

        _policyStoreMock.Setup(s => s.GetByWorkspaceIdAsync(workspaceId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { policy });

        var handler = new GetGovernancePoliciesQueryHandler(_policyStoreMock.Object, _tenantContextMock.Object);

        // Act
        var result = await handler.HandleAsync(new GetGovernancePoliciesQuery(workspaceId));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Protect Emails");
        result.Value[0].MaskingStrategy.Should().Be("Email");
    }

    [Fact]
    public async Task ToggleGovernancePolicy_UpdatesStatusAndAudits()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        _tenantContextMock.Setup(t => t.WorkspaceId).Returns(workspaceId);

        var policy = GovernancePolicy.Create(
            workspaceId: workspaceId,
            name: "Hide Passwords",
            description: "Nullify password hashes",
            targetTable: "users",
            targetColumn: "password_hash",
            classification: DataClassification.Secret,
            maskingStrategy: MaskingStrategy.Nullify,
            isActive: true);

        typeof(GovernancePolicy).GetProperty("Id")!.SetValue(policy, policyId);

        _policyStoreMock.Setup(s => s.GetByIdAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var handler = new ToggleGovernancePolicyCommandHandler(
            _policyStoreMock.Object,
            _auditStoreMock.Object,
            _tenantContextMock.Object);

        // Act
        var result = await handler.HandleAsync(new ToggleGovernancePolicyCommand(workspaceId, userId, policyId, IsActive: false));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();

        _policyStoreMock.Verify(s => s.UpdateAsync(policy, It.IsAny<CancellationToken>()), Times.Once);
        _auditStoreMock.Verify(a => a.AddAsync(It.Is<AuditLog>(l =>
            l.Action == "Governance.TogglePolicy"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestMaskingPreview_ReturnsMaskedPreviewResult()
    {
        // Arrange
        var handler = new TestMaskingPreviewQueryHandler(_maskingEngine);
        var query = new TestMaskingPreviewQuery("user@company.org", "Email");

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OriginalValue.Should().Be("user@company.org");
        result.Value.MaskedValue.Should().Be("u***@company.org");
        result.Value.MaskingStrategy.Should().Be("Email");
    }
}
