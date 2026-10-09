using IntelliQueryAI.Domain.Entities.Scenarios;

namespace IntelliQueryAI.Domain.Services;

public sealed record SimulationResult(
    decimal BaselineValue,
    decimal ExpectedValue,
    decimal BestCaseValue,
    decimal WorstCaseValue,
    decimal ExpectedDeltaValue,
    decimal ExpectedDeltaPercentage,
    IReadOnlyList<ScenarioProjection> Projections,
    IReadOnlyList<MonthlyDataPoint> Trajectory,
    double ConfidenceScore);

public interface IScenarioSimulationEngine
{
    SimulationResult CalculateSimulation(
        decimal baselineValue,
        string timeHorizon,
        IReadOnlyList<ScenarioVariable> variables);
}

public sealed class ScenarioSimulationEngine : IScenarioSimulationEngine
{
    public SimulationResult CalculateSimulation(
        decimal baselineValue,
        string timeHorizon,
        IReadOnlyList<ScenarioVariable> variables)
    {
        int months = ResolveMonths(timeHorizon);

        // 1. Calculate weighted net compound change rate
        decimal netChangePercentage = 0m;
        foreach (var v in variables)
        {
            decimal weightedChange = v.ChangePercentage * (decimal)v.Weight;
            netChangePercentage += weightedChange;
        }

        // 2. Expected End-of-Horizon Value
        decimal expectedFactor = 1m + (netChangePercentage / 100m);
        decimal expectedValue = Math.Round(baselineValue * expectedFactor, 2);
        decimal expectedDelta = expectedValue - baselineValue;
        decimal expectedDeltaPct = baselineValue != 0
            ? Math.Round((expectedDelta / baselineValue) * 100m, 2)
            : 0m;

        // 3. Best-Case (Upper Bound with favorable tailwind: +15% boost on positive factors or stress relief)
        decimal bestFactor = 1m + ((netChangePercentage + Math.Max(10m, Math.Abs(netChangePercentage) * 0.35m)) / 100m);
        decimal bestCaseValue = Math.Round(baselineValue * bestFactor, 2);
        decimal bestDelta = bestCaseValue - baselineValue;
        decimal bestDeltaPct = baselineValue != 0 ? Math.Round((bestDelta / baselineValue) * 100m, 2) : 0m;

        // 4. Worst-Case (Pessimistic Stress Test with headwind: -15% down shock)
        decimal worstFactor = 1m + ((netChangePercentage - Math.Max(10m, Math.Abs(netChangePercentage) * 0.35m)) / 100m);
        decimal worstCaseValue = Math.Round(baselineValue * worstFactor, 2);
        decimal worstDelta = worstCaseValue - baselineValue;
        decimal worstDeltaPct = baselineValue != 0 ? Math.Round((worstDelta / baselineValue) * 100m, 2) : 0m;

        // 5. Generate Monthly Step Trajectory
        var trajectory = new List<MonthlyDataPoint>(months);
        var expectedMonthlyGrowthRate = months > 0
            ? (double)netChangePercentage / 100.0 / months
            : 0.0;
        var bestMonthlyGrowthRate = months > 0
            ? (double)(netChangePercentage + Math.Max(10m, Math.Abs(netChangePercentage) * 0.35m)) / 100.0 / months
            : 0.0;
        var worstMonthlyGrowthRate = months > 0
            ? (double)(netChangePercentage - Math.Max(10m, Math.Abs(netChangePercentage) * 0.35m)) / 100.0 / months
            : 0.0;

        DateTime startDate = DateTime.UtcNow;

        for (int m = 1; m <= months; m++)
        {
            string monthLabel = startDate.AddMonths(m).ToString("MMM yyyy");
            decimal baseVal = baselineValue; // Steady state baseline
            decimal expVal = Math.Round(baselineValue * (decimal)(1.0 + (expectedMonthlyGrowthRate * m)), 2);
            decimal bestVal = Math.Round(baselineValue * (decimal)(1.0 + (bestMonthlyGrowthRate * m)), 2);
            decimal worstVal = Math.Round(baselineValue * (decimal)(1.0 + (worstMonthlyGrowthRate * m)), 2);

            trajectory.Add(new MonthlyDataPoint(
                Month: monthLabel,
                Baseline: baseVal,
                Projected: expVal,
                BestCase: bestVal,
                WorstCase: worstVal));
        }

        // 6. Assemble Case Projections
        var projections = new List<ScenarioProjection>
        {
            new ScenarioProjection(
                CaseType: "Baseline",
                ProjectedValue: baselineValue,
                DeltaValue: 0m,
                DeltaPercentage: 0m,
                MonthlyBreakdown: trajectory),

            new ScenarioProjection(
                CaseType: "Expected",
                ProjectedValue: expectedValue,
                DeltaValue: expectedDelta,
                DeltaPercentage: expectedDeltaPct,
                MonthlyBreakdown: trajectory),

            new ScenarioProjection(
                CaseType: "BestCase",
                ProjectedValue: bestCaseValue,
                DeltaValue: bestDelta,
                DeltaPercentage: bestDeltaPct,
                MonthlyBreakdown: trajectory),

            new ScenarioProjection(
                CaseType: "WorstCase",
                ProjectedValue: worstCaseValue,
                DeltaValue: worstDelta,
                DeltaPercentage: worstDeltaPct,
                MonthlyBreakdown: trajectory)
        };

        // 7. Statistical Confidence calculation based on number of variables and magnitude of shift
        double volatilityPenalty = Math.Min(0.25, (double)Math.Abs(netChangePercentage) / 200.0);
        double variableSpreadPenalty = Math.Min(0.15, variables.Count * 0.02);
        double confidence = Math.Clamp(0.92 - volatilityPenalty - variableSpreadPenalty, 0.55, 0.95);

        return new SimulationResult(
            BaselineValue: baselineValue,
            ExpectedValue: expectedValue,
            BestCaseValue: bestCaseValue,
            WorstCaseValue: worstCaseValue,
            ExpectedDeltaValue: expectedDelta,
            ExpectedDeltaPercentage: expectedDeltaPct,
            Projections: projections,
            Trajectory: trajectory,
            ConfidenceScore: Math.Round(confidence, 2));
    }

    private static int ResolveMonths(string timeHorizon)
    {
        if (string.IsNullOrWhiteSpace(timeHorizon)) return 12;

        string lower = timeHorizon.ToLowerInvariant();
        if (lower.Contains("quarter") || lower.Contains("3 month")) return 3;
        if (lower.Contains("6 month") || lower.Contains("half")) return 6;
        if (lower.Contains("year") || lower.Contains("12 month")) return 12;
        if (lower.Contains("24 month") || lower.Contains("2 year")) return 24;

        return 12;
    }
}
