using System.Text.RegularExpressions;

namespace IntelliQueryAI.AI.Evaluations;

public sealed record EvaluationResult(
    bool IsValid,
    double ConfidenceScore,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Recommendations);

public interface IAgentEvaluationService
{
    EvaluationResult EvaluateSql(string sql, IReadOnlyCollection<string> validTableNames, IReadOnlyCollection<string> validColumnNames);
    EvaluationResult EvaluateDataInsights(int rowCount, IReadOnlyList<string> insights);
}

public sealed class AgentEvaluationService : IAgentEvaluationService
{
    private static readonly string[] DangerousKeywords =
    [
        "DELETE", "UPDATE", "INSERT", "DROP", "TRUNCATE", "ALTER", "EXEC", "EXECUTE",
        "GRANT", "REVOKE", "MERGE", "CALL", "CREATE", "REPLACE"
    ];

    public EvaluationResult EvaluateSql(string sql, IReadOnlyCollection<string> validTableNames, IReadOnlyCollection<string> validColumnNames)
    {
        var issues = new List<string>();
        var recommendations = new List<string>();
        double confidence = 1.0;

        if (string.IsNullOrWhiteSpace(sql))
        {
            return new EvaluationResult(false, 0.0, ["Generated SQL statement is empty."], ["Ensure a SELECT query is formulated."]);
        }

        // 1. Strict single or non-destructive check
        string normalized = sql.Trim();
        foreach (var keyword in DangerousKeywords)
        {
            if (Regex.IsMatch(normalized, $@"\b{keyword}\b", RegexOptions.IgnoreCase))
            {
                issues.Add($"Prohibited destructive or mutating SQL command detected: '{keyword}'.");
                confidence -= 0.5;
            }
        }

        // 2. Must start with SELECT or WITH (for CTEs)
        if (!Regex.IsMatch(normalized, @"^(SELECT|WITH)\b", RegexOptions.IgnoreCase))
        {
            issues.Add("SQL query does not start with SELECT or WITH clause.");
            confidence -= 0.4;
        }

        // 3. Row Limit Check
        bool hasLimit = Regex.IsMatch(normalized, @"\b(LIMIT|TOP|FETCH\s+FIRST)\b", RegexOptions.IgnoreCase);
        if (!hasLimit)
        {
            recommendations.Add("No explicit row limit detected. Automatic safety LIMIT 1000 should be enforced.");
            confidence -= 0.05;
        }

        // 4. Multiple statement detection (semicolon followed by non-whitespace)
        if (Regex.IsMatch(normalized, @";\s*\S+"))
        {
            issues.Add("Multiple SQL statements chained together are forbidden to prevent injection attacks.");
            confidence -= 0.5;
        }

        // 5. Placeholder or conversational error message detection
        // Catches LLM outputs like "SELECT TOP (20) N'The provided schema does not include an EBS transactions table...' AS [Message];"
        bool isPlaceholderMessage =
            Regex.IsMatch(normalized, @"\b(provided\s+schema\s+does\s+not\s+include|please\s+provide|table\s+not\s+found|unknown\s+table|no\s+table\s+found|schema\s+does\s+not\s+contain)\b", RegexOptions.IgnoreCase) ||
            (!Regex.IsMatch(normalized, @"\bFROM\b", RegexOptions.IgnoreCase) && Regex.IsMatch(normalized, @"\bAS\s+\[?(Message|Error|Notice)\]?", RegexOptions.IgnoreCase));

        if (isPlaceholderMessage)
        {
            issues.Add("The generated SQL statement is an explanatory message indicating missing schema entities rather than an executable data query.");
            confidence = 0.0;
        }

        confidence = Math.Max(0.0, Math.Min(1.0, confidence));
        bool isValid = issues.Count == 0;

        return new EvaluationResult(isValid, confidence, issues, recommendations);
    }

    public EvaluationResult EvaluateDataInsights(int rowCount, IReadOnlyList<string> insights)
    {
        var issues = new List<string>();
        var recommendations = new List<string>();
        double confidence = 1.0;

        if (rowCount == 0)
        {
            recommendations.Add("Query returned 0 rows. Verify filter criteria or date ranges.");
            confidence = 0.7;
        }

        if (insights.Count == 0)
        {
            issues.Add("No actionable insights were derived from the data.");
            confidence -= 0.2;
        }

        return new EvaluationResult(issues.Count == 0, confidence, issues, recommendations);
    }
}
