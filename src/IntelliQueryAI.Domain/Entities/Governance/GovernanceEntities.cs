using System.Text.Json;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Governance;

/// <summary>
/// Domain entity representing an enterprise data access and column masking governance rule.
/// Strictly controls sensitive data visibility for user roles and prevents sensitive data leakage to AI context.
/// </summary>
public sealed class GovernancePolicy : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string TargetTable { get; private set; } = string.Empty;
    public string TargetColumn { get; private set; } = string.Empty;
    public DataClassification Classification { get; private set; }
    public MaskingStrategy MaskingStrategy { get; private set; }
    public string? MaskingPattern { get; private set; }
    public string AllowedRolesJson { get; private set; } = "[\"Owner\",\"Admin\"]";
    public bool IsActive { get; private set; }
    public bool IsAiMaskingEnforced { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private GovernancePolicy(
        Guid workspaceId,
        string name,
        string? description,
        string targetTable,
        string targetColumn,
        DataClassification classification,
        MaskingStrategy maskingStrategy,
        string? maskingPattern,
        string allowedRolesJson,
        bool isActive,
        bool isAiMaskingEnforced)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        Name = name.Trim();
        Description = description?.Trim();
        TargetTable = string.IsNullOrWhiteSpace(targetTable) ? "*" : targetTable.Trim();
        TargetColumn = string.IsNullOrWhiteSpace(targetColumn) ? "*" : targetColumn.Trim();
        Classification = classification;
        MaskingStrategy = maskingStrategy;
        MaskingPattern = maskingPattern?.Trim();
        AllowedRolesJson = string.IsNullOrWhiteSpace(allowedRolesJson) ? "[\"Owner\",\"Admin\"]" : allowedRolesJson.Trim();
        IsActive = isActive;
        IsAiMaskingEnforced = isAiMaskingEnforced;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private GovernancePolicy() { }

    public static GovernancePolicy Create(
        Guid workspaceId,
        string name,
        string? description,
        string targetTable,
        string targetColumn,
        DataClassification classification,
        MaskingStrategy maskingStrategy,
        string? maskingPattern = null,
        string? allowedRolesJson = null,
        bool isActive = true,
        bool isAiMaskingEnforced = true)
    {
        return new GovernancePolicy(
            workspaceId,
            name,
            description,
            targetTable,
            targetColumn,
            classification,
            maskingStrategy,
            maskingPattern,
            allowedRolesJson ?? "[\"Owner\",\"Admin\"]",
            isActive,
            isAiMaskingEnforced);
    }

    public void Update(
        string name,
        string? description,
        string targetTable,
        string targetColumn,
        DataClassification classification,
        MaskingStrategy maskingStrategy,
        string? maskingPattern,
        string? allowedRolesJson,
        bool isActive,
        bool isAiMaskingEnforced)
    {
        Name = name.Trim();
        Description = description?.Trim();
        TargetTable = string.IsNullOrWhiteSpace(targetTable) ? "*" : targetTable.Trim();
        TargetColumn = string.IsNullOrWhiteSpace(targetColumn) ? "*" : targetColumn.Trim();
        Classification = classification;
        MaskingStrategy = maskingStrategy;
        MaskingPattern = maskingPattern?.Trim();
        AllowedRolesJson = string.IsNullOrWhiteSpace(allowedRolesJson) ? "[\"Owner\",\"Admin\"]" : allowedRolesJson.Trim();
        IsActive = isActive;
        IsAiMaskingEnforced = isAiMaskingEnforced;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ToggleActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool Matches(string? tableName, string columnName)
    {
        if (!IsActive) return false;

        bool tableMatch = string.Equals(TargetTable, "*", StringComparison.OrdinalIgnoreCase)
            || (tableName != null && string.Equals(TargetTable, tableName, StringComparison.OrdinalIgnoreCase));

        bool columnMatch = string.Equals(TargetColumn, "*", StringComparison.OrdinalIgnoreCase)
            || string.Equals(TargetColumn, columnName, StringComparison.OrdinalIgnoreCase);

        return tableMatch && columnMatch;
    }

    public bool IsRoleAllowed(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return false;

        try
        {
            var allowed = JsonSerializer.Deserialize<List<string>>(AllowedRolesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (allowed == null) return false;

            return allowed.Any(r => string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
