using System.Text.Json;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.BusinessKnowledge;

/// <summary>
/// Domain entity representing a standardized business definition, calculation condition,
/// and schema mapping within the multi-tenant Business Knowledge / Semantic Layer.
/// </summary>
public sealed class BusinessTerm : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Definition { get; private set; } = string.Empty;
    public string AliasesJson { get; private set; } = "[]";
    public string Category { get; private set; } = "General";
    public string Classification { get; private set; } = "Internal";
    public string? OwnerName { get; private set; }
    public string? OwnerEmail { get; private set; }
    public string? TargetTable { get; private set; }
    public string? TargetColumn { get; private set; }
    public string? SqlCalculationRule { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private BusinessTerm(
        Guid workspaceId,
        string name,
        string definition,
        string aliasesJson,
        string category,
        string classification,
        string? ownerName,
        string? ownerEmail,
        string? targetTable,
        string? targetColumn,
        string? sqlCalculationRule,
        bool isActive)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        Name = name.Trim();
        Definition = definition.Trim();
        AliasesJson = aliasesJson;
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
        Classification = string.IsNullOrWhiteSpace(classification) ? "Internal" : classification.Trim();
        OwnerName = ownerName?.Trim();
        OwnerEmail = ownerEmail?.Trim();
        TargetTable = targetTable?.Trim();
        TargetColumn = targetColumn?.Trim();
        SqlCalculationRule = sqlCalculationRule?.Trim();
        IsActive = isActive;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private BusinessTerm() { }

    public static BusinessTerm Create(
        Guid workspaceId,
        string name,
        string definition,
        IReadOnlyList<string>? aliases = null,
        string category = "General",
        string classification = "Internal",
        string? ownerName = null,
        string? ownerEmail = null,
        string? targetTable = null,
        string? targetColumn = null,
        string? sqlCalculationRule = null)
    {
        string aliasesJson = aliases is not null && aliases.Count > 0
            ? JsonSerializer.Serialize(aliases)
            : "[]";

        return new BusinessTerm(
            workspaceId,
            name,
            definition,
            aliasesJson,
            category,
            classification,
            ownerName,
            ownerEmail,
            targetTable,
            targetColumn,
            sqlCalculationRule,
            isActive: true);
    }

    public void Update(
        string name,
        string definition,
        IReadOnlyList<string>? aliases,
        string category,
        string classification,
        string? ownerName,
        string? ownerEmail,
        string? targetTable,
        string? targetColumn,
        string? sqlCalculationRule)
    {
        Name = name.Trim();
        Definition = definition.Trim();
        AliasesJson = aliases is not null && aliases.Count > 0
            ? JsonSerializer.Serialize(aliases)
            : "[]";
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
        Classification = string.IsNullOrWhiteSpace(classification) ? "Internal" : classification.Trim();
        OwnerName = ownerName?.Trim();
        OwnerEmail = ownerEmail?.Trim();
        TargetTable = targetTable?.Trim();
        TargetColumn = targetColumn?.Trim();
        SqlCalculationRule = sqlCalculationRule?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain entity representing a key performance indicator (KPI) or operational metric definition
/// with explicit mathematical formulation and schema mappings.
/// </summary>
public sealed class MetricDefinition : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public Guid? DataSourceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public string CalculationFormula { get; private set; } = string.Empty;
    public string? FilterExpression { get; private set; }
    public string? TargetTable { get; private set; }
    public string? TargetColumn { get; private set; }
    public string AggregationType { get; private set; } = "SUM";
    public string TrendDirection { get; private set; } = "HigherIsBetter";
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private MetricDefinition(
        Guid workspaceId,
        Guid? dataSourceId,
        string name,
        string description,
        string unit,
        string calculationFormula,
        string? filterExpression,
        string? targetTable,
        string? targetColumn,
        string aggregationType,
        string trendDirection,
        bool isActive)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        DataSourceId = dataSourceId;
        Name = name.Trim();
        Description = description.Trim();
        Unit = string.IsNullOrWhiteSpace(unit) ? string.Empty : unit.Trim();
        CalculationFormula = calculationFormula.Trim();
        FilterExpression = filterExpression?.Trim();
        TargetTable = targetTable?.Trim();
        TargetColumn = targetColumn?.Trim();
        AggregationType = string.IsNullOrWhiteSpace(aggregationType) ? "SUM" : aggregationType.Trim().ToUpperInvariant();
        TrendDirection = string.IsNullOrWhiteSpace(trendDirection) ? "HigherIsBetter" : trendDirection.Trim();
        IsActive = isActive;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private MetricDefinition() { }

    public static MetricDefinition Create(
        Guid workspaceId,
        string name,
        string description,
        string calculationFormula,
        string unit = "",
        Guid? dataSourceId = null,
        string? filterExpression = null,
        string? targetTable = null,
        string? targetColumn = null,
        string aggregationType = "SUM",
        string trendDirection = "HigherIsBetter")
    {
        return new MetricDefinition(
            workspaceId,
            dataSourceId,
            name,
            description,
            unit,
            calculationFormula,
            filterExpression,
            targetTable,
            targetColumn,
            aggregationType,
            trendDirection,
            isActive: true);
    }

    public void Update(
        string name,
        string description,
        string calculationFormula,
        string unit,
        Guid? dataSourceId,
        string? filterExpression,
        string? targetTable,
        string? targetColumn,
        string aggregationType,
        string trendDirection)
    {
        Name = name.Trim();
        Description = description.Trim();
        Unit = string.IsNullOrWhiteSpace(unit) ? string.Empty : unit.Trim();
        CalculationFormula = calculationFormula.Trim();
        DataSourceId = dataSourceId;
        FilterExpression = filterExpression?.Trim();
        TargetTable = targetTable?.Trim();
        TargetColumn = targetColumn?.Trim();
        AggregationType = string.IsNullOrWhiteSpace(aggregationType) ? "SUM" : aggregationType.Trim().ToUpperInvariant();
        TrendDirection = string.IsNullOrWhiteSpace(trendDirection) ? "HigherIsBetter" : trendDirection.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
