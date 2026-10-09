using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Ai.Dtos;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Ai.Queries.ResolveAiContext;

public sealed record ResolveAiContextQuery(
    Guid WorkspaceId,
    string ContextType,
    Guid? EntityId = null,
    Guid? DataSourceId = null,
    string? Metric = null,
    string? DateRange = null,
    string? EntityTitle = null,
    IReadOnlyDictionary<string, string>? AdditionalMetadata = null) : IQuery<Result<ResolvedAiContextDto>>;

public sealed class ResolveAiContextQueryValidator : AbstractValidator<ResolveAiContextQuery>
{
    private static readonly HashSet<string> ValidContextTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "kpi",
        "metric",
        "alert",
        "anomaly",
        "chart",
        "dashboard",
        "dashboardwidget",
        "report",
        "executive",
        "executivereport",
        "recommendation",
        "forecast",
        "customermetric",
        "customer",
        "investigation",
        "datasource",
        "schema"
    };

    public ResolveAiContextQueryValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("Workspace identifier is required.");
        RuleFor(x => x.ContextType)
            .NotEmpty().WithMessage("Context type is required.")
            .Must(t => ValidContextTypes.Contains(t))
            .WithMessage(x => $"Context type '{x.ContextType}' is not recognized. Valid types: {string.Join(", ", ValidContextTypes)}");
    }
}

public sealed class ResolveAiContextQueryHandler : IQueryHandler<ResolveAiContextQuery, Result<ResolvedAiContextDto>>
{
    private readonly ITenantContext _tenantContext;
    private readonly IWorkspaceStore _workspaceStore;
    private readonly IAlertStore _alertStore;
    private readonly IMonitorStore _monitorStore;
    private readonly IExecutiveReportStore _executiveReportStore;
    private readonly IInvestigationReportStore _investigationReportStore;
    private readonly IDataSourceStore _dataSourceStore;
    private readonly IMetricStore _metricStore;
    private readonly ISavedReportStore _savedReportStore;

    public ResolveAiContextQueryHandler(
        ITenantContext tenantContext,
        IWorkspaceStore workspaceStore,
        IAlertStore alertStore,
        IMonitorStore monitorStore,
        IExecutiveReportStore executiveReportStore,
        IInvestigationReportStore investigationReportStore,
        IDataSourceStore dataSourceStore,
        IMetricStore metricStore,
        ISavedReportStore savedReportStore)
    {
        _tenantContext = tenantContext;
        _workspaceStore = workspaceStore;
        _alertStore = alertStore;
        _monitorStore = monitorStore;
        _executiveReportStore = executiveReportStore;
        _investigationReportStore = investigationReportStore;
        _dataSourceStore = dataSourceStore;
        _metricStore = metricStore;
        _savedReportStore = savedReportStore;
    }

    public async Task<Result<ResolvedAiContextDto>> HandleAsync(
        ResolveAiContextQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Tenant validation
        if (_tenantContext.IsResolved && _tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextTenantMismatch);
        }

        var workspace = await _workspaceStore.GetByIdAsync(query.WorkspaceId, cancellationToken);
        if (workspace == null)
        {
            return Result.Failure<ResolvedAiContextDto>(WorkspaceErrors.NotFound);
        }

        string normalizedType = query.ContextType.Trim().ToLowerInvariant();
        var enrichedMeta = query.AdditionalMetadata != null
            ? new Dictionary<string, string>(query.AdditionalMetadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string resolvedTitle = query.EntityTitle ?? string.Empty;
        string? resolvedMetric = query.Metric;
        Guid? effectiveDataSourceId = query.DataSourceId;

        // 2. Entity validation & tenant isolation checks
        if (query.EntityId.HasValue)
        {
            var entityId = query.EntityId.Value;

            switch (normalizedType)
            {
                case "alert":
                case "anomaly":
                    var alert = await _alertStore.GetByIdAsync(entityId, cancellationToken);
                    if (alert == null)
                    {
                        return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextEntityNotFound("Alert", entityId));
                    }
                    if (alert.WorkspaceId != query.WorkspaceId)
                    {
                        return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextTenantMismatch);
                    }
                    resolvedTitle = string.IsNullOrWhiteSpace(resolvedTitle) ? alert.Title : resolvedTitle;
                    enrichedMeta["AlertSeverity"] = alert.Severity.ToString();
                    enrichedMeta["AlertType"] = alert.AlertType.ToString();
                    enrichedMeta["AlertDescription"] = alert.Description;
                    if (alert.MetricValue.HasValue) enrichedMeta["MetricValue"] = alert.MetricValue.Value.ToString("F2");
                    if (alert.ThresholdValue.HasValue) enrichedMeta["ThresholdValue"] = alert.ThresholdValue.Value.ToString("F2");

                    if (!effectiveDataSourceId.HasValue && alert.MonitorId.HasValue)
                    {
                        var monitor = await _monitorStore.GetByIdAsync(alert.MonitorId.Value, cancellationToken);
                        if (monitor != null && monitor.WorkspaceId == query.WorkspaceId)
                        {
                            effectiveDataSourceId = monitor.DataSourceId;
                        }
                    }
                    break;

                case "report":
                case "executive":
                case "executivereport":
                    var execReport = await _executiveReportStore.GetByIdAsync(entityId, cancellationToken);
                    if (execReport != null)
                    {
                        if (execReport.WorkspaceId != query.WorkspaceId)
                        {
                            return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextTenantMismatch);
                        }
                        resolvedTitle = string.IsNullOrWhiteSpace(resolvedTitle) ? execReport.Title : resolvedTitle;
                        enrichedMeta["ReportPeriod"] = execReport.ReportPeriod;
                        enrichedMeta["ReportSummary"] = execReport.Summary;
                        enrichedMeta["GeneratedAtUtc"] = execReport.GeneratedAtUtc.ToString("o");
                    }
                    else
                    {
                        var savedReport = await _savedReportStore.GetByIdAsync(entityId, cancellationToken);
                        if (savedReport == null)
                        {
                            return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextEntityNotFound("Report", entityId));
                        }
                        if (savedReport.WorkspaceId != query.WorkspaceId)
                        {
                            return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextTenantMismatch);
                        }
                        resolvedTitle = string.IsNullOrWhiteSpace(resolvedTitle) ? savedReport.Title : resolvedTitle;
                        enrichedMeta["ReportQuestion"] = savedReport.Question;
                        enrichedMeta["ReportSqlQuery"] = savedReport.SqlQuery;
                    }
                    break;

                case "investigation":
                    var invReport = await _investigationReportStore.GetByIdAsync(entityId, cancellationToken);
                    if (invReport == null)
                    {
                        return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextEntityNotFound("Investigation", entityId));
                    }
                    if (invReport.WorkspaceId != query.WorkspaceId)
                    {
                        return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextTenantMismatch);
                    }
                    resolvedTitle = string.IsNullOrWhiteSpace(resolvedTitle) ? invReport.Question : resolvedTitle;
                    resolvedMetric = string.IsNullOrWhiteSpace(resolvedMetric) ? invReport.TargetMetric : resolvedMetric;
                    if (!effectiveDataSourceId.HasValue && invReport.DataSourceId.HasValue)
                    {
                        effectiveDataSourceId = invReport.DataSourceId.Value;
                    }
                    enrichedMeta["ConfidenceScore"] = invReport.ConfidenceScore.ToString("P0");
                    enrichedMeta["Summary"] = invReport.Summary;
                    enrichedMeta["Status"] = invReport.Status;
                    break;

                case "kpi":
                case "metric":
                    var metricDef = await _metricStore.GetByIdAsync(entityId, cancellationToken);
                    if (metricDef != null)
                    {
                        if (metricDef.WorkspaceId != query.WorkspaceId)
                        {
                            return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextTenantMismatch);
                        }
                        resolvedTitle = string.IsNullOrWhiteSpace(resolvedTitle) ? metricDef.Name : resolvedTitle;
                        resolvedMetric = metricDef.Name;
                        enrichedMeta["Formula"] = metricDef.CalculationFormula;
                        enrichedMeta["Aggregation"] = metricDef.AggregationType;
                        if (!string.IsNullOrWhiteSpace(metricDef.FilterExpression)) enrichedMeta["Filter"] = metricDef.FilterExpression;
                    }
                    break;

                case "datasource":
                case "schema":
                    var ds = await _dataSourceStore.GetByIdAsync(entityId, cancellationToken);
                    if (ds == null)
                    {
                        return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextEntityNotFound("DataSource", entityId));
                    }
                    if (ds.WorkspaceId != query.WorkspaceId)
                    {
                        return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextTenantMismatch);
                    }
                    effectiveDataSourceId = ds.Id;
                    resolvedTitle = string.IsNullOrWhiteSpace(resolvedTitle) ? ds.Name : resolvedTitle;
                    enrichedMeta["DataSourceType"] = ds.DatabaseType.ToString();
                    break;
            }
        }

        // 3. Fallback resolution for Metric definition by name if EntityId was not provided
        if ((normalizedType == "kpi" || normalizedType == "metric") && !string.IsNullOrWhiteSpace(resolvedMetric))
        {
            var metricDefs = await _metricStore.GetByWorkspaceIdAsync(query.WorkspaceId, search: resolvedMetric, cancellationToken: cancellationToken);
            var matched = metricDefs?.FirstOrDefault(m => string.Equals(m.Name, resolvedMetric, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                enrichedMeta["Formula"] = matched.CalculationFormula;
                enrichedMeta["Aggregation"] = matched.AggregationType;
                if (!string.IsNullOrWhiteSpace(matched.FilterExpression)) enrichedMeta["Filter"] = matched.FilterExpression;
            }
        }

        // 4. Default DataSource selection if not resolved
        if (!effectiveDataSourceId.HasValue)
        {
            var datasources = await _dataSourceStore.GetByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);
            var defaultDs = datasources?.FirstOrDefault(d => d.IsActive) ?? datasources?.FirstOrDefault();
            if (defaultDs != null)
            {
                effectiveDataSourceId = defaultDs.Id;
            }
        }
        else
        {
            // Verify dataSource ownership
            var ds = await _dataSourceStore.GetByIdAsync(effectiveDataSourceId.Value, cancellationToken);
            if (ds != null && ds.WorkspaceId != query.WorkspaceId)
            {
                return Result.Failure<ResolvedAiContextDto>(AiErrors.ContextTenantMismatch);
            }
        }

        // 5. Build intelligent contextual prompts and follow-up questions
        var (summary, prompt, followUps) = GenerateContextualQuestions(
            normalizedType,
            resolvedTitle,
            resolvedMetric,
            query.DateRange,
            enrichedMeta);

        return Result.Success(new ResolvedAiContextDto(
            IsValid: true,
            ContextType: normalizedType,
            WorkspaceId: query.WorkspaceId,
            EntityId: query.EntityId,
            DataSourceId: effectiveDataSourceId,
            Metric: resolvedMetric,
            DateRange: query.DateRange,
            EntityTitle: resolvedTitle,
            ContextSummary: summary,
            SuggestedPrompt: prompt,
            SuggestedFollowUpQuestions: followUps,
            EnrichedMetadata: enrichedMeta));
    }

    private static (string Summary, string Prompt, IReadOnlyList<string> FollowUps) GenerateContextualQuestions(
        string contextType,
        string entityTitle,
        string? metric,
        string? dateRange,
        IReadOnlyDictionary<string, string> metadata)
    {
        string label = !string.IsNullOrWhiteSpace(entityTitle)
            ? entityTitle
            : (!string.IsNullOrWhiteSpace(metric) ? metric : "Selected Element");

        string dateSuffix = !string.IsNullOrWhiteSpace(dateRange) ? $" for period {dateRange}" : string.Empty;

        switch (contextType)
        {
            case "kpi":
            case "metric":
                string kpiMetric = metric ?? label;
                return (
                    Summary: $"KPI: {kpiMetric}{dateSuffix}",
                    Prompt: $"Why did {kpiMetric} change{dateSuffix}? Analyze the primary contributors, period-over-period variance, and any anomalous transactions.",
                    FollowUps: new[]
                    {
                        $"What were the primary contributors to this change in {kpiMetric}?",
                        $"Compare {kpiMetric} against the 30-day moving average.",
                        $"Break down {kpiMetric} by top 5 contributing dimensions.",
                        $"Show seasonal trend and volatility patterns for {kpiMetric}."
                    });

            case "alert":
                string alertType = metadata.TryGetValue("AlertType", out var at) ? at : "Incident";
                string severity = metadata.TryGetValue("AlertSeverity", out var sev) ? sev : "Warning";
                return (
                    Summary: $"Alert: {label} [{severity} | {alertType}]",
                    Prompt: $"Explain this alert: '{label}'. What caused the trigger threshold to be breached and which data points contributed most?",
                    FollowUps: new[]
                    {
                        "Which records or dimensions triggered this threshold?",
                        "What is the historical occurrence frequency of this alert?",
                        "What immediate remediation actions are recommended?",
                        "Show baseline threshold versus current recorded value."
                    });

            case "anomaly":
                return (
                    Summary: $"Anomaly: {label}{dateSuffix}",
                    Prompt: $"Investigate this anomaly: '{label}'{dateSuffix}. Which records, accounts, or categories contributed most to this statistical deviation?",
                    FollowUps: new[]
                    {
                        "Which dimensions or segments drove the largest deviation?",
                        "Is this an isolated anomaly or an emerging trend?",
                        "Show the baseline distribution versus current recorded values.",
                        "What data quality or pipeline issues could cause this deviation?"
                    });

            case "chart":
            case "dashboard":
            case "dashboardwidget":
                return (
                    Summary: $"Dashboard Analysis: {label}",
                    Prompt: $"Explain the trend, inflection points, and statistical patterns observed in {label}{dateSuffix}.",
                    FollowUps: new[]
                    {
                        "What explains the most significant inflection point in this chart?",
                        "Break down performance across top contributing categories.",
                        "What is the projected trajectory for the next period?",
                        "Are there any outliers hidden within this aggregate chart?"
                    });

            case "report":
            case "executive":
            case "executivereport":
                return (
                    Summary: $"Report: {label}",
                    Prompt: $"Summarize executive report '{label}' and highlight the top 3 actionable insights for business stakeholders.",
                    FollowUps: new[]
                    {
                        "What are the critical risks highlighted in this report?",
                        "Which strategic opportunities have the highest expected return?",
                        "What recommended actions are highest priority?",
                        "Show the underlying query data supporting these conclusions."
                    });

            case "recommendation":
                return (
                    Summary: $"Recommendation: {label}",
                    Prompt: $"Show supporting evidence and underlying query data for recommendation '{label}'.",
                    FollowUps: new[]
                    {
                        "What is the quantitative impact of implementing this recommendation?",
                        "What risks arise if no corrective action is taken?",
                        "Provide a step-by-step query verification of the underlying assumptions.",
                        "What similar historical interventions have proven effective?"
                    });

            case "forecast":
                return (
                    Summary: $"Forecast: {label}",
                    Prompt: $"Explain the forecast model, assumptions, and key growth drivers for {label}.",
                    FollowUps: new[]
                    {
                        "What are the confidence interval bounds for this forecast?",
                        "Which underlying variables have the highest sensitivity?",
                        "What external factors could invalidate this forecast?",
                        "Compare this forecast against actual performance from the prior period."
                    });

            case "customermetric":
            case "customer":
                return (
                    Summary: $"Customer Analytics: {label}",
                    Prompt: $"Analyze customer performance for '{label}': examine cohort retention, volume behavior, and margin variance.",
                    FollowUps: new[]
                    {
                        "Which customer cohort has the highest churn risk?",
                        "Show the top 10% highest lifetime value accounts.",
                        "What usage patterns precede customer contract expansion?",
                        "Compare cohort retention over 30, 60, and 90 days."
                    });

            case "investigation":
                return (
                    Summary: $"Investigation: {label}",
                    Prompt: $"Review root-cause investigation '{label}' and explain the primary contributors and diagnostic evidence.",
                    FollowUps: new[]
                    {
                        "Show the exact SQL evidence executed during this investigation.",
                        "Can we isolate sub-segment contributors for this incident?",
                        "What preventative monitoring safeguards should be configured?",
                        "Recommend actionable remediation steps based on these findings."
                    });

            default:
                return (
                    Summary: $"Context: {label}",
                    Prompt: $"Analyze {label}{dateSuffix}. Provide summary insights, trend breakdown, and key metrics.",
                    FollowUps: new[]
                    {
                        "What are the primary drivers for this element?",
                        "Show the underlying data points.",
                        "Are there any anomalies or irregularities detected?"
                    });
        }
    }
}
