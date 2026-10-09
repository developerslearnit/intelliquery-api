using FluentAssertions;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Services;
using Xunit;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Domain.Tests;

public sealed class MonitoringAndAlertEntitiesTests
{
    [Fact]
    public void Monitor_Create_ShouldInitializeDefaultsCorrectly()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var dataSourceId = Guid.NewGuid();

        // Act
        var monitor = Monitor.Create(
            workspaceId,
            dataSourceId,
            "Revenue Drop Monitor",
            "Monitors daily revenue variance",
            MonitorType.Revenue,
            ScheduleType.Daily,
            "SELECT SUM(amount) FROM orders WHERE created_at >= NOW() - INTERVAL '1 day'",
            "DailyRevenue");

        // Assert
        monitor.Should().NotBeNull();
        monitor.WorkspaceId.Should().Be(workspaceId);
        monitor.DataSourceId.Should().Be(dataSourceId);
        monitor.Name.Should().Be("Revenue Drop Monitor");
        monitor.MonitorType.Should().Be(MonitorType.Revenue);
        monitor.ScheduleType.Should().Be(ScheduleType.Daily);
        monitor.IsActive.Should().BeTrue();
        monitor.Rules.Should().BeEmpty();
    }

    [Fact]
    public void Monitor_AddAndRemoveRule_ShouldUpdateRulesCollection()
    {
        // Arrange
        var monitor = Monitor.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Churn Monitor",
            null,
            MonitorType.CustomerChurn,
            ScheduleType.Weekly);

        var rule = MonitorRule.Create(
            monitor.Id,
            RuleType.PercentageChange,
            15.0m,
            ComparisonOperator.GreaterThanOrEqual,
            "7d");

        // Act
        monitor.AddRule(rule);

        // Assert
        monitor.Rules.Should().HaveCount(1);
        monitor.Rules.First().ThresholdValue.Should().Be(15.0m);

        // Act 2: Remove rule
        monitor.RemoveRule(rule.Id);
        monitor.Rules.Should().BeEmpty();
    }

    [Theory]
    [InlineData(120, 100, ComparisonOperator.GreaterThan, true)]
    [InlineData(90, 100, ComparisonOperator.GreaterThan, false)]
    [InlineData(80, 100, ComparisonOperator.LessThan, true)]
    [InlineData(100, 100, ComparisonOperator.GreaterThanOrEqual, true)]
    public void MonitorRule_EvaluateThreshold_ShouldReturnCorrectResult(
        decimal currentValue,
        decimal threshold,
        ComparisonOperator op,
        bool expectedResult)
    {
        // Arrange
        var rule = MonitorRule.Create(
            Guid.NewGuid(),
            RuleType.Threshold,
            threshold,
            op);

        // Act
        bool result = rule.Evaluate(currentValue);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void MonitorRule_EvaluatePercentageChange_ShouldCalculateDeltaAgainstBaseline()
    {
        // Baseline 100, Current 75 -> -25% change
        var rule = MonitorRule.Create(
            Guid.NewGuid(),
            RuleType.PercentageChange,
            -20.0m,
            ComparisonOperator.LessThanOrEqual);

        bool result = rule.Evaluate(currentValue: 75m, baselineValue: 100m);
        result.Should().BeTrue("A 25% drop is less than or equal to -20%");

        bool notTriggered = rule.Evaluate(currentValue: 95m, baselineValue: 100m);
        notTriggered.Should().BeFalse("A 5% drop does not violate -20% threshold");
    }

    [Fact]
    public void MonitorRule_EvaluateAbsenceOfData_ShouldTriggerWhenZero()
    {
        // Arrange
        var rule = MonitorRule.Create(
            Guid.NewGuid(),
            RuleType.AbsenceOfData,
            0m,
            ComparisonOperator.Equal);

        // Act & Assert
        rule.Evaluate(0m).Should().BeTrue();
        rule.Evaluate(150m).Should().BeFalse();
    }

    [Fact]
    public void MonitorExecution_StartAndComplete_ShouldTransitionState()
    {
        // Arrange
        var monitorId = Guid.NewGuid();
        var execution = MonitorExecution.Start(monitorId);

        execution.Status.Should().Be(ExecutionStatus.Running);
        execution.CompletedAtUtc.Should().BeNull();

        // Act
        execution.Complete(
            currentValue: 850.5m,
            baselineValue: 1000m,
            findings: "Revenue dropped 15%",
            generatedInsights: "{\"trend\":\"downward\"}",
            alertTriggered: true,
            durationMs: 350);

        // Assert
        execution.Status.Should().Be(ExecutionStatus.TriggeredAlert);
        execution.CurrentMetricValue.Should().Be(850.5m);
        execution.BaselineMetricValue.Should().Be(1000m);
        execution.CompletedAtUtc.Should().NotBeNull();
        execution.ExecutionDurationMs.Should().Be(350);
    }

    [Fact]
    public void Alert_CreateAndAcknowledge_ShouldUpdateStateCorrectly()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var monitorId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var alert = Alert.Create(
            workspaceId,
            monitorId,
            AlertType.ThresholdExceeded,
            AlertSeverity.Critical,
            "Critical Revenue Drop",
            "Daily revenue declined 28.5%",
            71.5m,
            100m);

        // Assert
        alert.IsAcknowledged.Should().BeFalse();
        alert.Severity.Should().Be(AlertSeverity.Critical);

        // Act 2: Acknowledge
        alert.Acknowledge(userId);

        // Assert 2
        alert.IsAcknowledged.Should().BeTrue();
        alert.AcknowledgedByUserId.Should().Be(userId);
        alert.AcknowledgedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void ExecutiveReport_Create_ShouldStoreBriefingContent()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();

        // Act
        var report = ExecutiveReport.Create(
            workspaceId,
            "Monthly Executive Briefing - October 2026",
            "October 2026",
            "Enterprise performance was resilient with 18% query growth.",
            "[\"Gross revenue surpassed $1.2M\"]",
            "[\"Churn increased 3.5%\"]",
            "[\"EMEA expansion is accelerating\"]",
            "[{\"title\":\"Retention Campaign\"}]");

        // Assert
        report.Should().NotBeNull();
        report.WorkspaceId.Should().Be(workspaceId);
        report.ReportPeriod.Should().Be("October 2026");
        report.Summary.Should().Contain("Enterprise performance was resilient");
        report.Findings.Should().Contain("Gross revenue surpassed");
        report.Risks.Should().Contain("Churn increased");
        report.GeneratedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}
