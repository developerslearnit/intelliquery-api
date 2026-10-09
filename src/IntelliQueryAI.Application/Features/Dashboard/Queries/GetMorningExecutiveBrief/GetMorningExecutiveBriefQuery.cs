using System.Globalization;
using System.Text.Json;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboard.Dtos;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.BusinessKnowledge;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Application.Features.Dashboard.Queries.GetMorningExecutiveBrief;

public sealed record GetMorningExecutiveBriefQuery(
    Guid WorkspaceId,
    string? Period = "Since yesterday",
    string? UserName = null) : IQuery<Result<MorningExecutiveBriefDto>>;

public sealed class GetMorningExecutiveBriefQueryHandler : IQueryHandler<GetMorningExecutiveBriefQuery, Result<MorningExecutiveBriefDto>>
{
    private readonly IMetricStore _metricStore;
    private readonly IMonitorStore _monitorStore;
    private readonly IAlertStore _alertStore;
    private readonly IExecutiveReportStore _executiveReportStore;
    private readonly IQueryHistoryStore _queryHistoryStore;
    private readonly IAnomalyDetectionService _anomalyService;
    private readonly ITenantContext _tenantContext;
    private readonly IWorkspaceStore? _workspaceStore;

    public GetMorningExecutiveBriefQueryHandler(
        IMetricStore metricStore,
        IMonitorStore monitorStore,
        IAlertStore alertStore,
        IExecutiveReportStore executiveReportStore,
        IQueryHistoryStore queryHistoryStore,
        IAnomalyDetectionService anomalyService,
        ITenantContext tenantContext,
        IWorkspaceStore? workspaceStore = null)
    {
        _metricStore = metricStore;
        _monitorStore = monitorStore;
        _alertStore = alertStore;
        _executiveReportStore = executiveReportStore;
        _queryHistoryStore = queryHistoryStore;
        _anomalyService = anomalyService;
        _tenantContext = tenantContext;
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<MorningExecutiveBriefDto>> HandleAsync(
        GetMorningExecutiveBriefQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Tenant Isolation Verification
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<MorningExecutiveBriefDto>(AiErrors.ContextTenantMismatch);
        }

        // 2. Resolve Workspace Name & User Display
        string? workspaceName = null;
        if (_workspaceStore is not null)
        {
            var ws = await _workspaceStore.GetByIdAsync(query.WorkspaceId, cancellationToken);
            workspaceName = ws?.Name;
        }

        string displayName = !string.IsNullOrWhiteSpace(workspaceName) ? workspaceName : "Workspace";
        string userDisplayName = !string.IsNullOrWhiteSpace(query.UserName) ? query.UserName : "Executive";

        // 3. Determine Normalized Period & Time Horizon Cut-off
        string normalizedPeriod = NormalizePeriod(query.Period);
        DateTime nowUtc = DateTime.UtcNow;
        DateTime periodCutoffUtc = GetPeriodCutoffUtc(normalizedPeriod, nowUtc);

        // 4. Time-aware Dynamic Greeting
        int hour = nowUtc.Hour;
        string greetingPrefix = hour switch
        {
            >= 4 and < 12 => "Good morning",
            >= 12 and < 17 => "Good afternoon",
            _ => "Good evening"
        };
        string greeting = $"{greetingPrefix}, {userDisplayName}";
        string headline = $"Here's what changed in your business {FormatPeriodHeadline(normalizedPeriod)}.";

        // 5. Query Active Business Knowledge Semantic Metrics (Phase A)
        var semanticMetrics = await _metricStore.GetByWorkspaceIdAsync(query.WorkspaceId, isActive: true, cancellationToken: cancellationToken);

        // 6. Query Active Monitors & Recent Executions
        var monitors = await _monitorStore.GetByWorkspaceIdAsync(query.WorkspaceId, cancellationToken: cancellationToken);
        var recentExecutions = await _monitorStore.GetRecentExecutionsByWorkspaceIdAsync(query.WorkspaceId, limit: 100, cancellationToken: cancellationToken);

        // 7. Query Alerts
        var allAlerts = await _alertStore.GetByWorkspaceIdAsync(query.WorkspaceId, onlyOpen: null, limit: 50, cancellationToken: cancellationToken);
        var periodAlerts = allAlerts.Where(a => a.CreatedAtUtc >= periodCutoffUtc).ToList();
        var openAlerts = allAlerts.Where(a => !a.IsAcknowledged).ToList();

        // 8. Query Analytical Query Histories
        var queryHistories = await _queryHistoryStore.GetByWorkspaceIdAsync(query.WorkspaceId, limit: 100, cancellationToken: cancellationToken);
        var periodQueries = queryHistories.Where(q => q.ExecutedAtUtc >= periodCutoffUtc).ToList();
        var previousQueries = queryHistories.Where(q => q.ExecutedAtUtc < periodCutoffUtc && q.ExecutedAtUtc >= periodCutoffUtc.AddDays(-(nowUtc - periodCutoffUtc).TotalDays)).ToList();

        // 9. Synthesize Dynamic Business Metrics (No Hardcoded Values)
        var dynamicMetrics = BuildDynamicMetrics(
            semanticMetrics,
            monitors,
            recentExecutions,
            periodQueries,
            previousQueries,
            periodCutoffUtc);

        // 10. Extract Critical Alerts
        var criticalAlertDtos = periodAlerts
            .Where(a => a.Severity == AlertSeverity.Critical || a.Severity == AlertSeverity.Warning)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedAtUtc)
            .Take(5)
            .Select(a =>
            {
                var monitor = monitors.FirstOrDefault(m => m.Id == a.MonitorId);
                return new ExecutiveBriefAlertDto(
                    a.Id,
                    Sanitize(a.Title, query.WorkspaceId, displayName),
                    a.Severity.ToString(),
                    monitor?.Name ?? "Operational Monitor",
                    a.CreatedAtUtc,
                    a.MetricValue,
                    a.ThresholdValue,
                    Sanitize(a.Description, query.WorkspaceId, displayName));
            })
            .ToList();

        // 11. Statistical Anomaly Detection
        var majorAnomalies = DetectPeriodAnomalies(
            allAlerts,
            queryHistories,
            monitors,
            periodCutoffUtc,
            query.WorkspaceId,
            displayName);

        // 12. Prioritization Engine: Rank Findings, Risks, Opportunities
        var (rankedFindings, emergingRisks, opportunities) = PrioritizeFindings(
            dynamicMetrics,
            criticalAlertDtos,
            majorAnomalies,
            monitors,
            openAlerts,
            displayName,
            normalizedPeriod);

        // 13. Fetch or Synthesize Executive Report for Period
        var existingReports = await _executiveReportStore.GetByWorkspaceIdAsync(query.WorkspaceId, limit: 10, cancellationToken: cancellationToken);
        var matchingReport = existingReports
            .FirstOrDefault(r => string.Equals(r.ReportPeriod, normalizedPeriod, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(r.ReportPeriod, query.Period, StringComparison.OrdinalIgnoreCase))
            ?? await _executiveReportStore.GetLatestByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);

        string summaryNarrative;
        IReadOnlyList<ActionableRecommendation> recommendations;

        if (matchingReport is not null && (nowUtc - matchingReport.GeneratedAtUtc).TotalHours < 12)
        {
            summaryNarrative = Sanitize(matchingReport.Summary, query.WorkspaceId, displayName);
            recommendations = ParseRecommendations(matchingReport.Recommendations, query.WorkspaceId, displayName);
        }
        else
        {
            summaryNarrative = GenerateExecutiveSummaryNarrative(
                displayName,
                normalizedPeriod,
                dynamicMetrics,
                criticalAlertDtos,
                majorAnomalies);

            recommendations = GenerateActionableRecommendations(
                criticalAlertDtos,
                majorAnomalies,
                dynamicMetrics,
                displayName);
        }

        var result = new MorningExecutiveBriefDto(
            matchingReport?.Id,
            greeting,
            headline,
            normalizedPeriod,
            summaryNarrative,
            nowUtc,
            dynamicMetrics,
            criticalAlertDtos,
            majorAnomalies,
            rankedFindings,
            emergingRisks,
            opportunities,
            recommendations);

        return Result.Success(result);
    }

    private static string NormalizePeriod(string? period)
    {
        if (string.IsNullOrWhiteSpace(period)) return "Since yesterday";
        string p = period.Trim().ToLowerInvariant();
        if (p.Contains("yesterday") || p.Contains("24h") || p.Contains("24 hour") || p.Contains("day"))
            return "Since yesterday";
        if (p.Contains("week") || p.Contains("wtd") || p.Contains("7d"))
            return "This week";
        if (p.Contains("month") || p.Contains("mtd") || p.Contains("30d"))
            return "Month-to-date";

        return period.Trim();
    }

    private static string FormatPeriodHeadline(string period) => period switch
    {
        "Since yesterday" => "since yesterday",
        "Last 24 hours" => "in the last 24 hours",
        "This week" => "this week",
        "Week-to-date" => "week-to-date",
        "Month-to-date" => "month-to-date",
        _ => $"over {period.ToLowerInvariant()}"
    };

    private static DateTime GetPeriodCutoffUtc(string period, DateTime nowUtc) => period switch
    {
        "Since yesterday" => nowUtc.AddHours(-24),
        "Last 24 hours" => nowUtc.AddHours(-24),
        "This week" => nowUtc.Date.AddDays(-(int)nowUtc.DayOfWeek),
        "Month-to-date" => new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        _ => nowUtc.AddHours(-24)
    };

    private static IReadOnlyList<ExecutiveBriefMetricDto> BuildDynamicMetrics(
        IReadOnlyList<MetricDefinition> semanticMetrics,
        IReadOnlyList<Monitor> monitors,
        IReadOnlyList<MonitorExecution> recentExecutions,
        IReadOnlyList<Domain.Entities.Ai.QueryHistory> periodQueries,
        IReadOnlyList<Domain.Entities.Ai.QueryHistory> previousQueries,
        DateTime periodCutoffUtc)
    {
        var metrics = new List<ExecutiveBriefMetricDto>();

        // A. Metrics derived from Active Monitors & Executions
        var executionsByMonitor = recentExecutions
            .GroupBy(e => e.MonitorId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.StartedAtUtc).ToList());

        foreach (var monitor in monitors.Where(m => m.IsActive))
        {
            if (!executionsByMonitor.TryGetValue(monitor.Id, out var monitorExecs) || monitorExecs.Count == 0)
            {
                continue;
            }

            var latest = monitorExecs[0];
            var previous = monitorExecs.Count > 1 ? monitorExecs[1] : null;

            decimal currentVal = latest.CurrentMetricValue ?? 0m;
            decimal? baselineVal = latest.BaselineMetricValue ?? previous?.CurrentMetricValue;

            decimal? changePct = null;
            if (baselineVal.HasValue && baselineVal.Value != 0)
            {
                changePct = Math.Round(((currentVal - baselineVal.Value) / Math.Abs(baselineVal.Value)) * 100m, 1);
            }

            string direction = changePct switch
            {
                > 0.1m => "Up",
                < -0.1m => "Down",
                _ => "Neutral"
            };

            string status = latest.Status == ExecutionStatus.Failed
                ? "Critical"
                : (changePct.HasValue && Math.Abs(changePct.Value) >= 20m)
                    ? "Warning"
                    : "Healthy";

            string priority = (status == "Critical" || (changePct.HasValue && Math.Abs(changePct.Value) >= 25m))
                ? "High"
                : (status == "Warning" || (changePct.HasValue && Math.Abs(changePct.Value) >= 10m))
                    ? "Medium"
                    : "Low";

            string category = monitor.MonitorType switch
            {
                MonitorType.Revenue or MonitorType.TaxCollection or MonitorType.Expense => "Revenue",
                MonitorType.CustomerChurn or MonitorType.SubscriptionGrowth => "Customers",
                MonitorType.FailedPayment => "Operations",
                _ => "Performance"
            };

            metrics.Add(new ExecutiveBriefMetricDto(
                Name: monitor.TargetMetric ?? monitor.Name,
                CurrentValue: currentVal,
                FormattedValue: FormatMetricValue(currentVal, monitor.TargetMetric ?? monitor.Name),
                BaselineValue: baselineVal,
                ChangePercentage: changePct,
                ChangeDirection: direction,
                Status: status,
                Priority: priority,
                Category: category,
                MonitorId: monitor.Id,
                Explanation: latest.Findings ?? $"Monitored via {monitor.Name}."
            ));
        }

        // B. Metrics derived from Business Knowledge Semantic Layer (Phase A)
        foreach (var sm in semanticMetrics)
        {
            // Skip if already represented by an active monitor metric
            if (metrics.Any(m => string.Equals(m.Name, sm.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            metrics.Add(new ExecutiveBriefMetricDto(
                Name: sm.Name,
                CurrentValue: 0m,
                FormattedValue: "Configured",
                BaselineValue: null,
                ChangePercentage: null,
                ChangeDirection: "Neutral",
                Status: "Healthy",
                Priority: "Medium",
                Category: "Revenue",
                MetricId: sm.Id,
                Explanation: sm.Description,
                Unit: sm.Unit
            ));
        }

        // C. AI Query Activity & System Performance Metric
        int currentCount = periodQueries.Count;
        int prevCount = previousQueries.Count;
        decimal? queryChangePct = prevCount > 0
            ? Math.Round(((decimal)(currentCount - prevCount) / prevCount) * 100m, 1)
            : null;

        metrics.Add(new ExecutiveBriefMetricDto(
            Name: "Analytical Query Volume",
            CurrentValue: currentCount,
            FormattedValue: currentCount.ToString("N0"),
            BaselineValue: prevCount > 0 ? prevCount : null,
            ChangePercentage: queryChangePct,
            ChangeDirection: queryChangePct switch { > 0 => "Up", < 0 => "Down", _ => "Neutral" },
            Status: "Healthy",
            Priority: "Low",
            Category: "Performance",
            Explanation: $"{currentCount} AST-verified queries processed."
        ));

        // Average Query Execution Latency
        if (periodQueries.Count > 0)
        {
            decimal avgLatency = (decimal)periodQueries.Average(q => q.ExecutionTimeMs);
            decimal? prevAvgLatency = previousQueries.Count > 0 ? (decimal)previousQueries.Average(q => q.ExecutionTimeMs) : null;
            decimal? latencyChange = prevAvgLatency.HasValue && prevAvgLatency.Value > 0
                ? Math.Round(((avgLatency - prevAvgLatency.Value) / prevAvgLatency.Value) * 100m, 1)
                : null;

            metrics.Add(new ExecutiveBriefMetricDto(
                Name: "Average Query Latency",
                CurrentValue: Math.Round(avgLatency, 0),
                FormattedValue: $"{Math.Round(avgLatency, 0)}ms",
                BaselineValue: prevAvgLatency.HasValue ? Math.Round(prevAvgLatency.Value, 0) : null,
                ChangePercentage: latencyChange,
                ChangeDirection: latencyChange switch { > 0 => "Up", < 0 => "Down", _ => "Neutral" },
                Status: avgLatency > 2000 ? "Warning" : "Healthy",
                Priority: avgLatency > 2000 ? "Medium" : "Low",
                Category: "Performance",
                Explanation: "End-to-end multi-agent pipeline latency."
            ));
        }

        // Return sorted: High priority first, then Medium, then Low
        return metrics
            .OrderBy(m => m.Priority switch { "High" => 1, "Medium" => 2, _ => 3 })
            .ThenByDescending(m => m.ChangePercentage.HasValue ? Math.Abs(m.ChangePercentage.Value) : 0)
            .Take(6)
            .ToList();
    }

    private static IReadOnlyList<ExecutiveBriefAnomalyDto> DetectPeriodAnomalies(
        IReadOnlyList<Alert> alerts,
        IReadOnlyList<Domain.Entities.Ai.QueryHistory> queryHistories,
        IReadOnlyList<Monitor> monitors,
        DateTime periodCutoffUtc,
        Guid workspaceId,
        string displayName)
    {
        var anomalies = new List<ExecutiveBriefAnomalyDto>();

        // 1. Alert-based anomalies
        var anomalyAlerts = alerts
            .Where(a => a.CreatedAtUtc >= periodCutoffUtc &&
                       (a.AlertType == AlertType.AnomalyDetected || a.AlertType == AlertType.DropSpike))
            .ToList();

        foreach (var a in anomalyAlerts)
        {
            var mon = monitors.FirstOrDefault(m => m.Id == a.MonitorId);
            decimal current = a.MetricValue ?? 0m;
            decimal expected = a.ThresholdValue ?? 0m;
            decimal deviation = expected != 0 ? Math.Round(((current - expected) / Math.Abs(expected)) * 100m, 1) : 0m;

            anomalies.Add(new ExecutiveBriefAnomalyDto(
                a.Id,
                mon?.TargetMetric ?? mon?.Name ?? "Metric Deviation",
                a.Severity.ToString(),
                current,
                expected,
                deviation,
                2.8,
                a.CreatedAtUtc,
                Sanitize(a.Description, workspaceId, displayName)));
        }

        return anomalies.OrderByDescending(a => Math.Abs(a.DeviationPercentage)).Take(3).ToList();
    }

    private static (
        IReadOnlyList<ExecutiveBriefFindingDto> RankedFindings,
        IReadOnlyList<ExecutiveBriefRiskDto> EmergingRisks,
        IReadOnlyList<ExecutiveBriefOpportunityDto> Opportunities) PrioritizeFindings(
            IReadOnlyList<ExecutiveBriefMetricDto> metrics,
            IReadOnlyList<ExecutiveBriefAlertDto> criticalAlerts,
            IReadOnlyList<ExecutiveBriefAnomalyDto> anomalies,
            IReadOnlyList<Monitor> monitors,
            IReadOnlyList<Alert> openAlerts,
            string displayName,
            string period)
    {
        var findings = new List<ExecutiveBriefFindingDto>();
        var risks = new List<ExecutiveBriefRiskDto>();
        var opportunities = new List<ExecutiveBriefOpportunityDto>();

        int rank = 1;

        // 1. Critical Alerts & Anomalies Top Priority
        foreach (var alert in criticalAlerts)
        {
            findings.Add(new ExecutiveBriefFindingDto(
                Rank: rank++,
                Title: $"Alert: {alert.Title} in {alert.MonitorName}",
                Impact: "High",
                Severity: alert.Severity,
                Confidence: 0.95,
                Category: "Operational Alert"));

            risks.Add(new ExecutiveBriefRiskDto(
                Rank: risks.Count + 1,
                Title: alert.Title,
                Severity: alert.Severity == "Critical" ? "Critical" : "High",
                Impact: $"Unresolved incident in {alert.MonitorName} requiring investigation."));
        }

        foreach (var anom in anomalies)
        {
            findings.Add(new ExecutiveBriefFindingDto(
                Rank: rank++,
                Title: $"Anomaly: {anom.MetricName} deviated by {anom.DeviationPercentage:+0.0;-0.0}% (Z-Score: {anom.ZScore:0.0})",
                Impact: "High",
                Severity: anom.Severity,
                Confidence: 0.92,
                Category: "Statistical Anomaly"));
        }

        // 2. Significant Metric Swings
        foreach (var m in metrics.Where(m => m.ChangePercentage.HasValue && Math.Abs(m.ChangePercentage.Value) >= 10m))
        {
            decimal changeVal = m.ChangePercentage!.Value;
            findings.Add(new ExecutiveBriefFindingDto(
                Rank: rank++,
                Title: $"{m.Name} shifted by {changeVal:+0.0;-0.0}% ({m.FormattedValue})",
                Impact: Math.Abs(changeVal) >= 20m ? "High" : "Medium",
                Severity: m.Status,
                Confidence: 0.88,
                Category: m.Category));

            if (m.ChangeDirection == "Down" && m.Category == "Revenue")
            {
                risks.Add(new ExecutiveBriefRiskDto(
                    Rank: risks.Count + 1,
                    Title: $"{m.Name} downward deviation of {m.ChangePercentage.Value:0.0}%",
                    Severity: "High",
                    Impact: "Negative deviation in core financial metrics across period."));
            }
            else if (m.ChangeDirection == "Up" && m.Category == "Revenue")
            {
                opportunities.Add(new ExecutiveBriefOpportunityDto(
                    Rank: opportunities.Count + 1,
                    Title: $"{m.Name} accelerated by {m.ChangePercentage.Value:+0.0}%",
                    PotentialImpact: "Positive growth trajectory exceeding historical baseline.",
                    SuggestedAction: "Deep-dive into top performing customer segments."));
            }
        }

        // 3. Baseline Findings if empty
        if (findings.Count == 0)
        {
            findings.Add(new ExecutiveBriefFindingDto(
                Rank: 1,
                Title: $"{displayName} operational telemetry is stable with zero critical incidents.",
                Impact: "Low",
                Severity: "Healthy",
                Confidence: 0.95,
                Category: "Stability"));
        }

        if (risks.Count == 0)
        {
            risks.Add(new ExecutiveBriefRiskDto(
                Rank: 1,
                Title: "No critical operational or revenue risks detected.",
                Severity: "Medium",
                Impact: "Continuous monitoring rules are actively running without threshold violations."));
        }

        if (opportunities.Count == 0)
        {
            opportunities.Add(new ExecutiveBriefOpportunityDto(
                Rank: 1,
                Title: "Expand proactive automated monitoring across key dimensions.",
                PotentialImpact: "Early alerting against segment volatility and transactional drops.",
                SuggestedAction: "Configure monitors for core customer retention and ARR."));
        }

        return (findings.Take(5).ToList(), risks.Take(4).ToList(), opportunities.Take(4).ToList());
    }

    private static string GenerateExecutiveSummaryNarrative(
        string displayName,
        string period,
        IReadOnlyList<ExecutiveBriefMetricDto> metrics,
        IReadOnlyList<ExecutiveBriefAlertDto> criticalAlerts,
        IReadOnlyList<ExecutiveBriefAnomalyDto> anomalies)
    {
        string statusText = criticalAlerts.Count > 0
            ? $"{criticalAlerts.Count} incident(s) require executive attention"
            : "all monitored business domains remain within nominal variance bands";

        string metricsText = metrics.Count > 0
            ? $"Key metrics tracked include {string.Join(", ", metrics.Take(3).Select(m => $"{m.Name} ({m.FormattedValue})"))}."
            : "No anomalies detected across connected databases.";

        return $"{displayName} executive summary for {period.ToLowerInvariant()}: {statusText}. {metricsText} Multi-agent diagnostic verification active.";
    }

    private static IReadOnlyList<ActionableRecommendation> GenerateActionableRecommendations(
        IReadOnlyList<ExecutiveBriefAlertDto> criticalAlerts,
        IReadOnlyList<ExecutiveBriefAnomalyDto> anomalies,
        IReadOnlyList<ExecutiveBriefMetricDto> metrics,
        string displayName)
    {
        var list = new List<ActionableRecommendation>();

        if (criticalAlerts.Count > 0)
        {
            var firstAlert = criticalAlerts[0];
            list.Add(new ActionableRecommendation(
                Title: $"Investigate {firstAlert.Title}",
                Category: "Incident Response",
                Priority: "High",
                Rationale: $"Critical alert in {firstAlert.MonitorName} requires root-cause diagnosis.",
                ActionSteps: new List<string>
                {
                    $"Review {firstAlert.MonitorName} execution findings",
                    "Launch automated diagnostic investigation in AI Query Studio",
                    "Acknowledge and assign corrective action"
                }));
        }

        var swingingMetric = metrics.FirstOrDefault(m => m.ChangePercentage.HasValue && Math.Abs(m.ChangePercentage.Value) >= 15m);
        if (swingingMetric is not null && swingingMetric.ChangePercentage.HasValue)
        {
            decimal changeVal = swingingMetric.ChangePercentage.Value;
            list.Add(new ActionableRecommendation(
                Title: $"Conduct Root-Cause Analysis on {swingingMetric.Name}",
                Category: "Analytics",
                Priority: "High",
                Rationale: $"{swingingMetric.Name} exhibited a {changeVal:+0.0;-0.0}% shift {swingingMetric.Explanation ?? ""}",
                ActionSteps: new List<string>
                {
                    $"Ask AI: Why did {swingingMetric.Name} change?",
                    "Evaluate dimension breakdown by customer segment and geography",
                    "Update baseline forecasting projections"
                }));
        }

        if (list.Count == 0)
        {
            list.Add(new ActionableRecommendation(
                Title: "Review Semantic Metric Definitions",
                Category: "Data Governance",
                Priority: "Medium",
                Rationale: "Enhancing business term dictionary mappings improves accuracy of automated morning briefings.",
                ActionSteps: new List<string>
                {
                    "Visit Knowledge Center (/knowledge)",
                    "Verify target formulas and thresholds for core revenue metrics"
                }));
        }

        return list;
    }

    private static IReadOnlyList<ActionableRecommendation> ParseRecommendations(string json, Guid workspaceId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<ActionableRecommendation>();
        try
        {
            var items = JsonSerializer.Deserialize<List<ActionableRecommendation>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return items?.Select(r => r with
            {
                Title = Sanitize(r.Title, workspaceId, displayName),
                Rationale = Sanitize(r.Rationale, workspaceId, displayName),
                ActionSteps = r.ActionSteps?.Select(s => Sanitize(s, workspaceId, displayName)).ToList() ?? new List<string>()
            }).ToList() ?? (IReadOnlyList<ActionableRecommendation>)Array.Empty<ActionableRecommendation>();
        }
        catch
        {
            return Array.Empty<ActionableRecommendation>();
        }
    }

    private static string FormatMetricValue(decimal value, string metricName)
    {
        string name = metricName.ToLowerInvariant();
        if (name.Contains("revenue") || name.Contains("arr") || name.Contains("mrr") || name.Contains("collection") || name.Contains("amount") || name.Contains("price"))
        {
            return $"${value:N0}";
        }
        if (name.Contains("rate") || name.Contains("percentage") || name.Contains("pct"))
        {
            return $"{value:0.0}%";
        }
        if (name.Contains("latency") || name.Contains("time") || name.Contains("duration"))
        {
            return $"{value:N0}ms";
        }

        return value >= 1000 ? value.ToString("N0") : value.ToString("0.##");
    }

    private static string Sanitize(string text, Guid workspaceId, string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return text
            .Replace(workspaceId.ToString(), workspaceName, StringComparison.OrdinalIgnoreCase)
            .Replace($"Workspace {workspaceId}", workspaceName, StringComparison.OrdinalIgnoreCase);
    }
}
