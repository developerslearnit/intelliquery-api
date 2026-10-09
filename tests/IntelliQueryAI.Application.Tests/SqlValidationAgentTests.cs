using FluentAssertions;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Evaluations;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class SqlValidationAgentTests
{
    private readonly SqlValidationAgent _validationAgent;

    public SqlValidationAgentTests()
    {
        var evalService = new AgentEvaluationService();
        var telemetryMock = new Mock<ITelemetryService>();
        var loggerMock = new Mock<ILogger<SqlValidationAgent>>();
        var optionsMock = Options.Create(new AzureOpenAISettings());
        var simulationMode = new SimulationMode(true);

        _validationAgent = new SqlValidationAgent(
            null,
            evalService,
            telemetryMock.Object,
            loggerMock.Object,
            optionsMock,
            simulationMode);
    }

    [Theory]
    [InlineData("DELETE FROM customers WHERE id = 1")]
    [InlineData("UPDATE customers SET is_active = false")]
    [InlineData("INSERT INTO orders (id, total) VALUES (1, 100)")]
    [InlineData("DROP TABLE financial_records")]
    [InlineData("TRUNCATE TABLE audit_logs")]
    [InlineData("ALTER TABLE users ADD COLUMN secret_key text")]
    [InlineData("EXEC sp_msforeachtable 'DROP TABLE ?'")]
    [InlineData("MERGE INTO target USING source ON target.id = source.id WHEN MATCHED THEN DELETE")]
    public void Validate_WhenDangerousCommandDetected_ShouldBlockQuery(string dangerousSql)
    {
        // Act
        var result = _validationAgent.Validate(dangerousSql, DatabaseType.PostgreSQL);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.BlockedReasons.Should().NotBeEmpty();
        result.SanitizedSql.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenMultipleStatementsChained_ShouldBlockQuery()
    {
        // Arrange
        var sql = "SELECT * FROM customers; DROP TABLE orders;";

        // Act
        var result = _validationAgent.Validate(sql, DatabaseType.PostgreSQL);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.BlockedReasons.Should().Contain(r => r.Contains("Multiple SQL statements"));
    }

    [Fact]
    public void Validate_WhenValidSelectProvided_ShouldAllowAndRetainQuery()
    {
        // Arrange
        var sql = "SELECT id, name, email FROM customers WHERE is_active = true ORDER BY name LIMIT 50;";

        // Act
        var result = _validationAgent.Validate(sql, DatabaseType.PostgreSQL);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.SanitizedSql.Should().Contain("SELECT id, name, email FROM customers");
        result.ConfidenceScore.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void Validate_WhenSelectHasNoRowLimit_ShouldAutomaticallyInjectLimit()
    {
        // Arrange
        var pgSql = "SELECT id, name FROM customers";
        var sqlServerSql = "SELECT id, name FROM customers";

        // Act
        var pgResult = _validationAgent.Validate(pgSql, DatabaseType.PostgreSQL);
        var sqlServerResult = _validationAgent.Validate(sqlServerSql, DatabaseType.SqlServer);

        // Assert
        pgResult.IsAllowed.Should().BeTrue();
        pgResult.SanitizedSql.Should().EndWith("LIMIT 1000;");

        sqlServerResult.IsAllowed.Should().BeTrue();
        sqlServerResult.SanitizedSql.Should().StartWith("SELECT TOP 1000");
    }

    [Fact]
    public void Validate_WhenSqlWrappedInMarkdownCodeBlocks_ShouldStripMarkdownAndValidate()
    {
        // Arrange
        var markdownSql = "```sql\nSELECT COUNT(*) as total FROM orders;\n```";

        // Act
        var result = _validationAgent.Validate(markdownSql, DatabaseType.PostgreSQL);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.SanitizedSql.Should().NotContain("```");
        result.SanitizedSql.Should().Contain("SELECT COUNT(*) as total FROM orders");
    }
}
