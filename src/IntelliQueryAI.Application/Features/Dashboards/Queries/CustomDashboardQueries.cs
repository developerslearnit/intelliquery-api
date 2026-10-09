using System.Text.Json;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboards.Dtos;
using IntelliQueryAI.Domain.Entities.Dashboards;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Dashboards.Queries;

// 1. Get All Custom Dashboards for Workspace
public sealed record GetCustomDashboardsQuery(
    Guid WorkspaceId,
    int Limit = 50) : IQuery<Result<IReadOnlyList<CustomDashboardDto>>>;

public sealed class GetCustomDashboardsQueryHandler : IQueryHandler<GetCustomDashboardsQuery, Result<IReadOnlyList<CustomDashboardDto>>>
{
    private readonly ICustomDashboardStore _dashboardStore;
    private readonly ITenantContext _tenantContext;

    public GetCustomDashboardsQueryHandler(
        ICustomDashboardStore dashboardStore,
        ITenantContext tenantContext)
    {
        _dashboardStore = dashboardStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<CustomDashboardDto>>> HandleAsync(
        GetCustomDashboardsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<IReadOnlyList<CustomDashboardDto>>(AiErrors.ContextTenantMismatch);
        }

        var dashboards = await _dashboardStore.GetByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);
        var dtos = dashboards.Take(query.Limit).Select(MapToDto).ToList();

        return Result.Success<IReadOnlyList<CustomDashboardDto>>(dtos);
    }

    internal static CustomDashboardDto MapToDto(CustomDashboard d)
    {
        IReadOnlyList<DashboardWidgetDto> widgets = Array.Empty<DashboardWidgetDto>();
        try
        {
            if (!string.IsNullOrWhiteSpace(d.LayoutJson))
            {
                widgets = JsonSerializer.Deserialize<List<DashboardWidgetDto>>(
                    d.LayoutJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<DashboardWidgetDto>();
            }
        }
        catch
        {
            widgets = Array.Empty<DashboardWidgetDto>();
        }

        return new CustomDashboardDto(
            d.Id,
            d.WorkspaceId,
            d.CreatedByUserId,
            d.Title,
            d.Description,
            d.Prompt,
            d.DateRange,
            d.IsDefault,
            d.IsShared,
            widgets,
            d.CreatedAtUtc,
            d.UpdatedAtUtc);
    }
}

// 2. Get Custom Dashboard By Id (with optional widget data hydration)
public sealed record GetCustomDashboardByIdQuery(
    Guid WorkspaceId,
    Guid DashboardId,
    bool HydrateData = false,
    Guid? DataSourceId = null) : IQuery<Result<CustomDashboardDto>>;

public sealed class GetCustomDashboardByIdQueryHandler : IQueryHandler<GetCustomDashboardByIdQuery, Result<CustomDashboardDto>>
{
    private readonly ICustomDashboardStore _dashboardStore;
    private readonly IDashboardCoordinator _dashboardCoordinator;
    private readonly ITenantContext _tenantContext;

    public GetCustomDashboardByIdQueryHandler(
        ICustomDashboardStore dashboardStore,
        IDashboardCoordinator dashboardCoordinator,
        ITenantContext tenantContext)
    {
        _dashboardStore = dashboardStore;
        _dashboardCoordinator = dashboardCoordinator;
        _tenantContext = tenantContext;
    }

    public async Task<Result<CustomDashboardDto>> HandleAsync(
        GetCustomDashboardByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.ContextTenantMismatch);
        }

        var dashboard = await _dashboardStore.GetByIdAsync(query.DashboardId, cancellationToken);
        if (dashboard is null)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.DashboardNotFound);
        }

        if (dashboard.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.DashboardTenantMismatch);
        }

        var baseDto = GetCustomDashboardsQueryHandler.MapToDto(dashboard);

        if (!query.HydrateData || baseDto.Widgets.Count == 0)
        {
            return Result.Success(baseDto);
        }

        // Hydrate widgets that have SQL queries
        var hydratedWidgets = new List<DashboardWidgetDto>();
        foreach (var w in baseDto.Widgets)
        {
            if (string.IsNullOrWhiteSpace(w.SqlQuery))
            {
                hydratedWidgets.Add(w);
                continue;
            }

            var execReq = new ExecuteWidgetQueryRequest(
                query.WorkspaceId,
                query.DataSourceId,
                w.SqlQuery,
                RowLimit: 50);

            var execResult = await _dashboardCoordinator.ExecuteWidgetQueryAsync(execReq, cancellationToken);
            if (execResult.IsSuccess)
            {
                var resp = execResult.Value;
                decimal? metricVal = w.MetricValue;

                // If it's a metric card and first row has a numeric column, derive metricVal
                if (w.WidgetType.Equals("metric", StringComparison.OrdinalIgnoreCase) && resp.Rows.Count > 0)
                {
                    var firstRow = resp.Rows[0];
                    foreach (var val in firstRow.Values)
                    {
                        if (val is not null && decimal.TryParse(val.ToString(), out var parsed))
                        {
                            metricVal = parsed;
                            break;
                        }
                    }
                }

                hydratedWidgets.Add(w with
                {
                    Columns = resp.Columns,
                    Rows = resp.Rows,
                    MetricValue = metricVal
                });
            }
            else
            {
                hydratedWidgets.Add(w);
            }
        }

        return Result.Success(baseDto with { Widgets = hydratedWidgets });
    }
}

// 3. Execute Single Dashboard Widget Query
public sealed record ExecuteDashboardWidgetQuery(
    Guid WorkspaceId,
    Guid DashboardId,
    string WidgetId,
    Guid? DataSourceId = null) : IQuery<Result<DashboardWidgetDto>>;

public sealed class ExecuteDashboardWidgetQueryHandler : IQueryHandler<ExecuteDashboardWidgetQuery, Result<DashboardWidgetDto>>
{
    private readonly ICustomDashboardStore _dashboardStore;
    private readonly IDashboardCoordinator _dashboardCoordinator;
    private readonly ITenantContext _tenantContext;

    public ExecuteDashboardWidgetQueryHandler(
        ICustomDashboardStore dashboardStore,
        IDashboardCoordinator dashboardCoordinator,
        ITenantContext tenantContext)
    {
        _dashboardStore = dashboardStore;
        _dashboardCoordinator = dashboardCoordinator;
        _tenantContext = tenantContext;
    }

    public async Task<Result<DashboardWidgetDto>> HandleAsync(
        ExecuteDashboardWidgetQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<DashboardWidgetDto>(AiErrors.ContextTenantMismatch);
        }

        var dashboard = await _dashboardStore.GetByIdAsync(query.DashboardId, cancellationToken);
        if (dashboard is null)
        {
            return Result.Failure<DashboardWidgetDto>(AiErrors.DashboardNotFound);
        }

        if (dashboard.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<DashboardWidgetDto>(AiErrors.DashboardTenantMismatch);
        }

        var baseDto = GetCustomDashboardsQueryHandler.MapToDto(dashboard);
        var widget = baseDto.Widgets.FirstOrDefault(w => string.Equals(w.Id, query.WidgetId, StringComparison.OrdinalIgnoreCase));
        if (widget is null)
        {
            return Result.Failure<DashboardWidgetDto>(Error.NotFound("Widget.NotFound", $"Widget '{query.WidgetId}' not found."));
        }

        if (string.IsNullOrWhiteSpace(widget.SqlQuery))
        {
            return Result.Success(widget);
        }

        var execReq = new ExecuteWidgetQueryRequest(
            query.WorkspaceId,
            query.DataSourceId,
            widget.SqlQuery,
            RowLimit: 100);

        var execResult = await _dashboardCoordinator.ExecuteWidgetQueryAsync(execReq, cancellationToken);
        if (!execResult.IsSuccess)
        {
            return Result.Failure<DashboardWidgetDto>(execResult.Error);
        }

        var resp = execResult.Value;
        decimal? metricVal = widget.MetricValue;

        if (widget.WidgetType.Equals("metric", StringComparison.OrdinalIgnoreCase) && resp.Rows.Count > 0)
        {
            var firstRow = resp.Rows[0];
            foreach (var val in firstRow.Values)
            {
                if (val is not null && decimal.TryParse(val.ToString(), out var parsed))
                {
                    metricVal = parsed;
                    break;
                }
            }
        }

        return Result.Success(widget with
        {
            Columns = resp.Columns,
            Rows = resp.Rows,
            MetricValue = metricVal
        });
    }
}
