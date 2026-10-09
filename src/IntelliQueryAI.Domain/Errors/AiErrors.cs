using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Domain.Errors;

public static class AiErrors
{
    public static Error UnsafeSqlDetected(string operation) => Error.Validation(
        "Ai.UnsafeSqlDetected",
        $"Query validation blocked unsafe SQL operation: '{operation}'. Only read-only SELECT queries are permitted.");

    public static readonly Error OnlySelectAllowed = Error.Validation(
        "Ai.OnlySelectAllowed",
        "Only read-only SELECT queries are allowed. Multi-statement or non-SELECT operations are strictly prohibited.");

    public static readonly Error EmptyQuestion = Error.Validation(
        "Ai.EmptyQuestion",
        "The analytical question cannot be null or whitespace.");

    public static readonly Error SchemaRetrievalFailed = Error.Failure(
        "Ai.SchemaRetrievalFailed",
        "Failed to retrieve relevant schema embeddings from PgVector metadata store.");

    public static Error QueryExecutionFailed(string details) => Error.Failure(
        "Ai.QueryExecutionFailed",
        $"Failed to execute generated query against target data source: {details}");

    public static readonly Error ReportNotFound = Error.NotFound(
        "Ai.ReportNotFound",
        "The requested saved report was not found in the workspace.");

    public static readonly Error SessionNotFound = Error.NotFound(
        "Ai.SessionNotFound",
        "The requested conversation session was not found.");

    public static readonly Error AgentExecutionFailed = Error.Failure(
        "Ai.AgentExecutionFailed",
        "Agent execution encountered an unhandled error.");

    public static Error NoSchemaCataloged(string dataSourceName) => Error.Validation(
        "Ai.NoSchemaCataloged",
        $"Data source '{dataSourceName}' does not have any discovered tables or schema metadata. Automated schema sync was executed but found 0 tables. Please verify that user tables exist and that credentials have SELECT and VIEW DEFINITION permissions.");

    public static Error TableNotFoundInSchema(string details) => Error.Validation(
        "Ai.TableNotFoundInSchema",
        $"The requested entities were not found in the cataloged database schema: {details}");

    public static Error InvalidContext(string details) => Error.Validation(
        "Ai.InvalidContext",
        $"The provided contextual intelligence data is invalid: {details}");

    public static Error ContextEntityNotFound(string contextType, Guid entityId) => Error.NotFound(
        "Ai.ContextEntityNotFound",
        $"Referenced {contextType} entity '{entityId}' was not found in this workspace.");

    public static readonly Error ContextTenantMismatch = Error.Unauthorized(
        "Ai.ContextTenantMismatch",
        "Context validation failed: The referenced entity does not belong to the authenticated workspace.");

    public static readonly Error DashboardNotFound = Error.NotFound(
        "Ai.DashboardNotFound",
        "The requested custom dashboard was not found in the workspace.");

    public static readonly Error DashboardTenantMismatch = Error.Unauthorized(
        "Ai.DashboardTenantMismatch",
        "Dashboard operation rejected: The specified dashboard belongs to another workspace.");

    public static Error UnsupportedWidgetType(string widgetType) => Error.Validation(
        "Ai.UnsupportedWidgetType",
        $"The widget type '{widgetType}' is not supported. Supported widget types are: metric, lineChart, barChart, areaChart, pieChart, table.");

    public static readonly Error GovernancePolicyNotFound = Error.NotFound(
        "Governance.PolicyNotFound",
        "The requested data governance policy was not found in the workspace.");

    public static readonly Error GovernanceTenantMismatch = Error.Unauthorized(
        "Governance.TenantMismatch",
        "Governance operation rejected: The specified policy belongs to another workspace.");

    public static Error UnauthorizedDataFieldAccess(string fieldName) => Error.Unauthorized(
        "Governance.UnauthorizedDataFieldAccess",
        $"Access to sensitive data field '{fieldName}' is restricted by enterprise governance policy.");

    public static readonly Error ScenarioNotFound = Error.NotFound(
        "Scenario.NotFound",
        "The requested scenario analysis was not found in the workspace.");

    public static readonly Error ScenarioTenantMismatch = Error.Unauthorized(
        "Scenario.TenantMismatch",
        "Scenario operation rejected: The specified scenario belongs to another workspace.");

    public static Error InvalidScenarioVariables(string details) => Error.Validation(
        "Scenario.InvalidVariables",
        $"Scenario simulation rejected: {details}");

    public static readonly Error RecommendationNotFound = Error.NotFound(
        "Recommendation.NotFound",
        "The requested recommendation was not found in the workspace.");

    public static readonly Error RecommendationTenantMismatch = Error.Unauthorized(
        "Recommendation.TenantMismatch",
        "Recommendation operation rejected: The specified recommendation belongs to another workspace.");

    public static readonly Error RecommendationAlreadyApplied = Error.Validation(
        "Recommendation.AlreadyApplied",
        "The recommendation has already been applied.");

    public static readonly Error RecommendationAlreadyDismissed = Error.Validation(
        "Recommendation.AlreadyDismissed",
        "The recommendation has already been dismissed.");

    public static readonly Error ForecastNotFound = Error.NotFound(
        "Forecast.NotFound",
        "The requested forecast was not found in the workspace.");

    public static readonly Error ForecastTenantMismatch = Error.Unauthorized(
        "Forecast.TenantMismatch",
        "Forecast operation rejected: The specified forecast belongs to another workspace.");

    public static Error InsufficientHistoricalData(string details) => Error.Validation(
        "Forecast.InsufficientHistoricalData",
        $"Forecast computation requires at least 2 historical observations: {details}");
}

