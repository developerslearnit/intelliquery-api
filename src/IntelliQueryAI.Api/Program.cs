using System.Text.Json.Serialization;
using IntelliQueryAI.AI;
using IntelliQueryAI.Api.Endpoints;
using IntelliQueryAI.Api.Middleware;
using IntelliQueryAI.Application;
using IntelliQueryAI.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog Setup
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// 2. OpenTelemetry Tracing & Metrics
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });

// 3. Application, Infrastructure & AI Layer DI
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAgentFramework(builder.Configuration);

// 4. JSON Options & Serialization (Support string enums across all endpoints)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// 5. OpenAPI / Swagger Documentation
builder.Services.AddOpenApi();

// 5. CORS Configuration for Local & Production Frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 6. Global Exception Handling
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// 7. Request Logging
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("IntelliQueryAI API Documentation")
               .WithTheme(ScalarTheme.Moon);
    });
}

app.UseCors();

// 8. Authentication & Tenant Resolution
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

// 9. Minimal API Route Mappings
app.MapGet("/", () => Results.Ok(new
{
    Platform = "IntelliQueryAI Enterprise Backend",
    Version = "1.0.0",
    Status = "Operational",
    Documentation = "/openapi/v1.json"
}));

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", TimestampUtc = DateTime.UtcNow }));

app.MapIdentityEndpoints();
app.MapWorkspaceEndpoints();
app.MapSubscriptionEndpoints();
app.MapAuditEndpoints();
app.MapDataSourceEndpoints();
app.MapSchemaEndpoints();
app.MapChatEndpoints();
app.MapReportEndpoints();

// Phase 5 Minimal APIs
app.MapAlertEndpoints();
app.MapMonitorEndpoints();
app.MapDashboardEndpoints();
app.MapExecutiveEndpoints();

// Phase A Minimal APIs
app.MapBusinessKnowledgeEndpoints();

// Phase B Minimal APIs
app.MapInvestigationEndpoints();

// Phase E Minimal APIs
app.MapCustomDashboardEndpoints();

// Phase F Minimal APIs
app.MapGovernanceEndpoints();

// Phase G Minimal APIs
app.MapScenarioEndpoints();

// Phase H Minimal APIs
app.MapRecommendationEndpoints();

// Phase I Minimal APIs
app.MapForecastEndpoints();

try
{
    Log.Information("Starting IntelliQueryAI API Server...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible to WebApplicationFactory for integration testing
public partial class Program { }
