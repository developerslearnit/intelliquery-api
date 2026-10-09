using System.Diagnostics;
using System.Text.Json;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Infrastructure.Background;

/// <summary>
/// Quartz.NET distributed-safe background job that executes active AI Monitors against connected data sources.
/// Evaluates rules, invokes the AI Monitoring Agent pipeline, and records audit trails.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MonitorExecutionJob : IJob
{
    private readonly IntelliQueryDbContext _context;
    private readonly IMonitorStore _monitorStore;
    private readonly IAlertStore _alertStore;
    private readonly IExecutiveCoordinator _executiveCoordinator;
    private readonly ISafeQueryExecutor _queryExecutor;
    private readonly IDataSourceStore _dataSourceStore;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuditLogStore _auditLogStore;
    private readonly ILogger<MonitorExecutionJob> _logger;

    public MonitorExecutionJob(
        IntelliQueryDbContext context,
        IMonitorStore monitorStore,
        IAlertStore alertStore,
        IExecutiveCoordinator executiveCoordinator,
        ISafeQueryExecutor queryExecutor,
        IDataSourceStore dataSourceStore,
        IEncryptionService encryptionService,
        IAuditLogStore auditLogStore,
        ILogger<MonitorExecutionJob> logger)
    {
        _context = context;
        _monitorStore = monitorStore;
        _alertStore = alertStore;
        _executiveCoordinator = executiveCoordinator;
        _queryExecutor = queryExecutor;
        _dataSourceStore = dataSourceStore;
        _encryptionService = encryptionService;
        _auditLogStore = auditLogStore;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Quartz background MonitorExecutionJob started at {UtcNow}", DateTime.UtcNow);

        // Fetch active monitors across all workspaces
        var activeMonitors = await _monitorStore.GetActiveMonitorsAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Found {Count} active monitors scheduled for evaluation", activeMonitors.Count);

        foreach (var monitor in activeMonitors)
        {
            var stopwatch = Stopwatch.StartNew();
            var execution = MonitorExecution.Start(monitor.Id);
            await _monitorStore.AddExecutionAsync(execution, cancellationToken);

            try
            {
                decimal currentMetricValue = 0m;
                decimal baselineMetricValue = 100m;
                bool queryExecuted = false;

                // If monitor has custom query template, attempt execution against target data source
                if (!string.IsNullOrWhiteSpace(monitor.QueryTemplate))
                {
                    var dataSource = await _dataSourceStore.GetByIdAsync(monitor.DataSourceId, cancellationToken);
                    if (dataSource is not null && dataSource.IsActive)
                    {
                        var decryptedConn = _encryptionService.Decrypt(dataSource.EncryptedConnectionString);
                        var queryResult = await _queryExecutor.ExecuteSafeQueryAsync(
                            dataSource.DatabaseType,
                            decryptedConn,
                            monitor.QueryTemplate,
                            15,
                            cancellationToken);

                        if (queryResult.IsSuccess && queryResult.Rows.Count > 0)
                        {
                            var firstRow = queryResult.Rows[0];
                            var firstVal = firstRow.Values.FirstOrDefault();
                            if (firstVal is not null && decimal.TryParse(firstVal.ToString(), out var parsedVal))
                            {
                                currentMetricValue = parsedVal;
                                queryExecuted = true;
                            }
                        }
                    }
                }

                if (!queryExecuted)
                {
                    // Fallback to recent query statistics / metric calculations for workspace
                    var recentCount = await _context.QueryHistories
                        .Where(q => q.WorkspaceId == monitor.WorkspaceId && q.ExecutedAtUtc >= DateTime.UtcNow.AddDays(-1))
                        .CountAsync(cancellationToken);

                    currentMetricValue = recentCount;
                    baselineMetricValue = Math.Max(10m, recentCount * 1.15m);
                }

                // Evaluate configured rules
                bool ruleBreached = false;
                foreach (var rule in monitor.Rules)
                {
                    if (rule.Evaluate(currentMetricValue, baselineMetricValue))
                    {
                        ruleBreached = true;
                        break;
                    }
                }

                // Invoke AI Monitoring Agent Pipeline
                var aiRequest = new EvaluateMonitorRequest(
                    monitor.WorkspaceId,
                    monitor.Id,
                    monitor.Name,
                    monitor.MonitorType,
                    currentMetricValue,
                    baselineMetricValue,
                    new List<decimal> { baselineMetricValue, currentMetricValue });

                var aiResult = await _executiveCoordinator.EvaluateMonitorAsync(aiRequest, cancellationToken);
                stopwatch.Stop();

                bool shouldTriggerAlert = ruleBreached || (aiResult.IsSuccess && aiResult.Value.TriggerAlert);
                var findings = aiResult.IsSuccess ? aiResult.Value.Findings : "Monitor evaluated within expected tolerances.";
                var insights = aiResult.IsSuccess
                    ? JsonSerializer.Serialize(new
                    {
                        aiResult.Value.TrendSummary,
                        aiResult.Value.Recommendations
                    })
                    : null;

                execution.Complete(
                    currentMetricValue,
                    baselineMetricValue,
                    findings,
                    insights,
                    shouldTriggerAlert,
                    stopwatch.ElapsedMilliseconds);

                await _context.SaveChangesAsync(cancellationToken);

                // If alert condition met, create intelligent Alert
                if (shouldTriggerAlert)
                {
                    var severity = aiResult.IsSuccess ? aiResult.Value.Severity : AlertSeverity.Warning;
                    var alert = Alert.Create(
                        monitor.WorkspaceId,
                        monitor.Id,
                        AlertType.ThresholdExceeded,
                        severity,
                        $"Monitor Triggered: {monitor.Name}",
                        findings,
                        currentMetricValue,
                        baselineMetricValue);

                    await _alertStore.AddAsync(alert, cancellationToken);

                    // Audit alert creation
                    await _auditLogStore.AddAsync(AuditLog.Create(
                        monitor.WorkspaceId,
                        Guid.Empty,
                        "Monitoring.AlertTriggered",
                        "Alert",
                        alert.Id.ToString(),
                        $"Alert raised for monitor '{monitor.Name}' (Severity: {severity})."),
                        cancellationToken);
                }

                _logger.LogInformation("Monitor {Id} ('{Name}') evaluated in {Duration}ms. Alert={Alert}",
                    monitor.Id, monitor.Name, stopwatch.ElapsedMilliseconds, shouldTriggerAlert);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error occurred during execution of monitor {Id} ('{Name}')", monitor.Id, monitor.Name);
                execution.Fail(ex.Message, stopwatch.ElapsedMilliseconds);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}

/// <summary>
/// Quartz.NET background job that inspects time-series metrics across active workspaces using statistical anomaly detection.
/// </summary>
[DisallowConcurrentExecution]
public sealed class AnomalyDetectionJob : IJob
{
    private readonly IntelliQueryDbContext _context;
    private readonly IAnomalyDetectionService _anomalyService;
    private readonly IAlertStore _alertStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly ILogger<AnomalyDetectionJob> _logger;

    public AnomalyDetectionJob(
        IntelliQueryDbContext context,
        IAnomalyDetectionService anomalyService,
        IAlertStore alertStore,
        IAuditLogStore auditLogStore,
        ILogger<AnomalyDetectionJob> logger)
    {
        _context = context;
        _anomalyService = anomalyService;
        _alertStore = alertStore;
        _auditLogStore = auditLogStore;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Quartz background AnomalyDetectionJob started at {UtcNow}", DateTime.UtcNow);

        var workspaces = await _context.Workspaces.ToListAsync(cancellationToken);

        foreach (var ws in workspaces)
        {
            try
            {
                // Gather recent hourly query volume data points over past 7 days
                var cutoff = DateTime.UtcNow.AddDays(-7);
                var queryPoints = await _context.QueryHistories
                    .Where(q => q.WorkspaceId == ws.Id && q.ExecutedAtUtc >= cutoff)
                    .GroupBy(q => new { q.ExecutedAtUtc.Date, q.ExecutedAtUtc.Hour })
                    .Select(g => new
                    {
                        Timestamp = new DateTime(g.Key.Date.Year, g.Key.Date.Month, g.Key.Date.Day, g.Key.Hour, 0, 0, DateTimeKind.Utc),
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Timestamp)
                    .ToListAsync(cancellationToken);

                if (queryPoints.Count >= 5)
                {
                    var dataPoints = queryPoints
                        .Select(p => new MetricDataPoint(p.Timestamp, p.Count, "HourlyQueryVolume"))
                        .ToList();

                    var anomalies = _anomalyService.AnalyzeSeries(dataPoints, zScoreThreshold: 2.5, percentageThreshold: 40.0m);

                    foreach (var anom in anomalies)
                    {
                        // Check deduplication: avoid duplicate alerts within the last 24h
                        var recentAlerts = await _alertStore.GetByWorkspaceIdAsync(ws.Id, onlyOpen: true, limit: 10, cancellationToken: cancellationToken);
                        bool alreadyAlerted = recentAlerts.Any(a => a.Description.Contains("HourlyQueryVolume", StringComparison.OrdinalIgnoreCase));

                        if (!alreadyAlerted)
                        {
                            var alert = Alert.Create(
                                ws.Id,
                                null,
                                anom.AlertType,
                                anom.Severity,
                                $"Anomaly Detected: Query Activity Variance",
                                anom.Description,
                                anom.CurrentValue,
                                anom.ExpectedValue);

                            await _alertStore.AddAsync(alert, cancellationToken);

                            await _auditLogStore.AddAsync(AuditLog.Create(
                                ws.Id,
                                Guid.Empty,
                                "Anomaly.Detected",
                                "Alert",
                                alert.Id.ToString(),
                                $"Statistical anomaly logged: {anom.Description}"),
                                cancellationToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running anomaly analysis for workspace {WorkspaceId}", ws.Id);
            }
        }
    }
}

/// <summary>
/// Quartz.NET background job that compiles weekly/monthly executive briefings using the Executive Intelligence Agent pipeline.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ExecutiveReportJob : IJob
{
    private readonly IntelliQueryDbContext _context;
    private readonly IExecutiveCoordinator _executiveCoordinator;
    private readonly IExecutiveReportStore _executiveStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly ILogger<ExecutiveReportJob> _logger;

    public ExecutiveReportJob(
        IntelliQueryDbContext context,
        IExecutiveCoordinator executiveCoordinator,
        IExecutiveReportStore executiveStore,
        IAuditLogStore auditLogStore,
        ILogger<ExecutiveReportJob> logger)
    {
        _context = context;
        _executiveCoordinator = executiveCoordinator;
        _executiveStore = executiveStore;
        _auditLogStore = auditLogStore;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Quartz background ExecutiveReportJob started at {UtcNow}", DateTime.UtcNow);

        var workspaces = await _context.Workspaces.ToListAsync(cancellationToken);
        string currentPeriod = DateTime.UtcNow.ToString("MMMM yyyy");

        foreach (var ws in workspaces)
        {
            try
            {
                // Verify if report for this period was already generated
                var existingReports = await _executiveStore.GetByWorkspaceIdAsync(ws.Id, limit: 5, cancellationToken: cancellationToken);
                bool exists = existingReports.Any(r => r.ReportPeriod.Equals(currentPeriod, StringComparison.OrdinalIgnoreCase)
                    && r.GeneratedAtUtc >= DateTime.UtcNow.AddDays(-7));

                if (exists)
                {
                    continue;
                }

                // Compile workspace telemetry metrics
                int totalQueries = await _context.QueryHistories.CountAsync(q => q.WorkspaceId == ws.Id, cancellationToken);
                int activeMonitors = await _context.Monitors.CountAsync(m => m.WorkspaceId == ws.Id && m.IsActive, cancellationToken);
                int openAlerts = await _context.Alerts.CountAsync(a => a.WorkspaceId == ws.Id && !a.IsAcknowledged, cancellationToken);

                var keyMetrics = new Dictionary<string, decimal>
                {
                    ["TotalHistoricalQueries"] = totalQueries,
                    ["ActiveMonitors"] = activeMonitors,
                    ["OpenAlerts"] = openAlerts
                };

                var recentAlerts = await _context.Alerts
                    .Where(a => a.WorkspaceId == ws.Id)
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .Take(5)
                    .Select(a => $"{a.Severity}: {a.Title}")
                    .ToListAsync(cancellationToken);

                var activeMonitorsList = await _context.Monitors
                    .Where(m => m.WorkspaceId == ws.Id && m.IsActive)
                    .Take(5)
                    .Select(m => $"{m.Name} ({m.MonitorType})")
                    .ToListAsync(cancellationToken);

                var req = new GenerateExecutiveReportRequest(
                    ws.Id,
                    currentPeriod,
                    $"{ws.Name} Executive Briefing - {currentPeriod}",
                    keyMetrics,
                    recentAlerts,
                    activeMonitorsList,
                    WorkspaceName: ws.Name);

                var res = await _executiveCoordinator.GenerateExecutiveBriefingAsync(req, cancellationToken);

                if (res.IsSuccess)
                {
                    var report = ExecutiveReport.Create(
                        ws.Id,
                        res.Value.Title,
                        res.Value.ReportPeriod,
                        res.Value.Summary,
                        JsonSerializer.Serialize(res.Value.KeyFindings),
                        JsonSerializer.Serialize(res.Value.Risks),
                        JsonSerializer.Serialize(res.Value.Opportunities),
                        JsonSerializer.Serialize(res.Value.Recommendations));

                    await _executiveStore.AddAsync(report, cancellationToken);

                    await _auditLogStore.AddAsync(AuditLog.Create(
                        ws.Id,
                        Guid.Empty,
                        "Executive.ReportGenerated",
                        "ExecutiveReport",
                        report.Id.ToString(),
                        $"Executive report '{res.Value.Title}' generated for period {currentPeriod}."),
                        cancellationToken);

                    _logger.LogInformation("Generated executive report {Id} for workspace {WorkspaceId}", report.Id, ws.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate periodic executive report for workspace {WorkspaceId}", ws.Id);
            }
        }
    }
}

/// <summary>
/// Quartz.NET background job that inspects unacknowledged alerts and handles SLA escalation reminders.
/// </summary>
[DisallowConcurrentExecution]
public sealed class AlertGenerationJob : IJob
{
    private readonly IntelliQueryDbContext _context;
    private readonly IAlertStore _alertStore;
    private readonly IAuditLogStore _auditLogStore;
    private readonly ILogger<AlertGenerationJob> _logger;

    public AlertGenerationJob(
        IntelliQueryDbContext context,
        IAlertStore alertStore,
        IAuditLogStore auditLogStore,
        ILogger<AlertGenerationJob> logger)
    {
        _context = context;
        _alertStore = alertStore;
        _auditLogStore = auditLogStore;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Quartz background AlertGenerationJob executing SLA scan at {UtcNow}", DateTime.UtcNow);

        // Find critical alerts unacknowledged for more than 48 hours
        var cutoff = DateTime.UtcNow.AddHours(-48);
        var unacknowledgedCritical = await _context.Alerts
            .Where(a => !a.IsAcknowledged && a.Severity == AlertSeverity.Critical && a.CreatedAtUtc <= cutoff)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var alert in unacknowledgedCritical)
        {
            _logger.LogWarning(
                "Critical alert {AlertId} in workspace {WorkspaceId} has remained unacknowledged since {CreatedAtUtc}",
                alert.Id, alert.WorkspaceId, alert.CreatedAtUtc);

            await _auditLogStore.AddAsync(AuditLog.Create(
                alert.WorkspaceId,
                Guid.Empty,
                "Alerts.SlaBreach",
                "Alert",
                alert.Id.ToString(),
                $"Critical alert '{alert.Title}' breached 48h acknowledgement SLA."),
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
