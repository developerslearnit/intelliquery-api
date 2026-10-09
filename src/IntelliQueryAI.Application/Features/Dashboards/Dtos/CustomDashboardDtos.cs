using System.Text.Json.Serialization;

namespace IntelliQueryAI.Application.Features.Dashboards.Dtos;

public sealed record DashboardWidgetDto(
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
    string? AdditionalOptionsJson = null,
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<IReadOnlyDictionary<string, object?>>? Rows = null,
    decimal? MetricValue = null,
    decimal? BaselineValue = null,
    decimal? ChangePercentage = null);

public sealed record CustomDashboardDto(
    Guid Id,
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string Title,
    string? Description,
    string? Prompt,
    string DateRange,
    bool IsDefault,
    bool IsShared,
    IReadOnlyList<DashboardWidgetDto> Widgets,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record GenerateDashboardFromPromptRequestDto(
    string Prompt,
    Guid? DataSourceId = null,
    string? DateRange = "Last 30 Days");

public sealed record CreateCustomDashboardRequestDto(
    string Title,
    string? Description = null,
    string? Prompt = null,
    string? DateRange = "Last 30 Days",
    IReadOnlyList<DashboardWidgetDto>? Widgets = null,
    bool IsDefault = false,
    bool IsShared = true);

public sealed record UpdateCustomDashboardRequestDto(
    string Title,
    string? Description = null,
    string? DateRange = "Last 30 Days",
    IReadOnlyList<DashboardWidgetDto>? Widgets = null,
    bool IsDefault = false,
    bool IsShared = true);
