using System.Text.Json;
using IntelliQueryAI.SharedKernel.Primitives;

namespace IntelliQueryAI.Domain.Entities.Dashboards;

/// <summary>
/// Domain aggregate root representing an AI-generated or custom-built analytical dashboard.
/// Stores structured widget definitions, grid layouts, and metadata without arbitrary code execution.
/// </summary>
public sealed class CustomDashboard : AggregateRoot
{
    public Guid WorkspaceId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Prompt { get; private set; }
    public string LayoutJson { get; private set; } = "[]";
    public string DateRange { get; private set; } = "Last 30 Days";
    public bool IsDefault { get; private set; }
    public bool IsShared { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private CustomDashboard(
        Guid workspaceId,
        Guid createdByUserId,
        string title,
        string? description,
        string? prompt,
        string layoutJson,
        string dateRange,
        bool isDefault,
        bool isShared)
        : base(Guid.NewGuid())
    {
        WorkspaceId = workspaceId;
        CreatedByUserId = createdByUserId;
        Title = title.Trim();
        Description = description?.Trim();
        Prompt = prompt?.Trim();
        LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? "[]" : layoutJson;
        DateRange = string.IsNullOrWhiteSpace(dateRange) ? "Last 30 Days" : dateRange.Trim();
        IsDefault = isDefault;
        IsShared = isShared;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private CustomDashboard() { }

    public static CustomDashboard Create(
        Guid workspaceId,
        Guid createdByUserId,
        string title,
        string? description = null,
        string? prompt = null,
        string? layoutJson = null,
        string dateRange = "Last 30 Days",
        bool isDefault = false,
        bool isShared = true)
    {
        return new CustomDashboard(
            workspaceId,
            createdByUserId,
            title,
            description,
            prompt,
            layoutJson ?? "[]",
            dateRange,
            isDefault,
            isShared);
    }

    public void Update(
        string title,
        string? description,
        string layoutJson,
        string dateRange,
        bool isDefault,
        bool isShared)
    {
        Title = title.Trim();
        Description = description?.Trim();
        LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? "[]" : layoutJson;
        DateRange = string.IsNullOrWhiteSpace(dateRange) ? "Last 30 Days" : dateRange.Trim();
        IsDefault = isDefault;
        IsShared = isShared;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetLayout(string layoutJson)
    {
        LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? "[]" : layoutJson;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Rename(string title, string? description)
    {
        Title = title.Trim();
        Description = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Structured widget definition specifying metrics, dimensions, chart types, and safe queries.
/// Strictly validated to prevent arbitrary script injection.
/// </summary>
public sealed record DashboardWidgetDefinition(
    string Id,
    string Title,
    string WidgetType,
    string Metric,
    string? Dimension = null,
    string? Aggregation = "SUM",
    string? SqlQuery = null,
    string? DateRange = null,
    int ColSpan = 1,
    int RowSpan = 1,
    string? AdditionalOptionsJson = null);

/// <summary>
/// Validator ensuring widget types match the whitelist of supported UI visualizations.
/// </summary>
public static class DashboardWidgetValidator
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "metric",
        "lineChart",
        "barChart",
        "areaChart",
        "pieChart",
        "table"
    };

    public static bool IsValidWidgetType(string? widgetType)
    {
        return !string.IsNullOrWhiteSpace(widgetType) && SupportedTypes.Contains(widgetType);
    }

    public static string NormalizeWidgetType(string? widgetType)
    {
        if (string.IsNullOrWhiteSpace(widgetType)) return "metric";
        string clean = widgetType.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
        return clean switch
        {
            "metric" or "kpi" or "number" or "card" => "metric",
            "linechart" or "line" or "timeseries" => "lineChart",
            "barchart" or "bar" or "column" => "barChart",
            "areachart" or "area" => "areaChart",
            "piechart" or "pie" or "donut" => "pieChart",
            "table" or "grid" or "list" => "table",
            _ => "metric"
        };
    }
}
