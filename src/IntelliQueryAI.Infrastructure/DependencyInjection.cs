using System.Text;
using IntelliQueryAI.Application.Abstractions.Services;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.Infrastructure.Authentication;
using IntelliQueryAI.Infrastructure.Dapper;
using IntelliQueryAI.Infrastructure.Persistence;
using IntelliQueryAI.Infrastructure.Persistence.Stores;
using IntelliQueryAI.Infrastructure.Services;
using IntelliQueryAI.Infrastructure.Services.Paystack;
using IntelliQueryAI.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using StackExchange.Redis;

namespace IntelliQueryAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Connection strings
        string postgresConnection = configuration.GetConnectionString("DefaultConnection")!;

        if(string.IsNullOrWhiteSpace(postgresConnection))
        {
            throw new InvalidOperationException("PostgreSQL connection string is not configured.");
        }

        //string? redisConnection = configuration.GetConnectionString("Redis");

        // 2. EF Core Database Context
        services.AddDbContext<IntelliQueryDbContext>((sp, options) =>
        {
            options.UseNpgsql(postgresConnection, npgsqlOptions =>
            {
                npgsqlOptions.UseVector();
                npgsqlOptions.MigrationsAssembly(typeof(IntelliQueryDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });
        });

        // 3. Dapper SQL Connection Factory
        services.AddSingleton<ISqlConnectionFactory>(_ => new NpgsqlConnectionFactory(postgresConnection));

        // 4. Multi-Tenant Scoped Context
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // 5. Domain Persistence Stores & Unit of Work
        services.AddScoped<IWorkspaceStore, EfWorkspaceStore>();
        services.AddScoped<IUserStore, EfUserStore>();
        services.AddScoped<IRefreshTokenStore, EfRefreshTokenStore>();
        services.AddScoped<ISubscriptionStore, EfSubscriptionStore>();
        services.AddScoped<IAuditLogStore, EfAuditLogStore>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        // Phase 2: DataSource Stores & Schema Read/Write Stores
        services.AddScoped<IDataSourceStore, EfDataSourceStore>();
        services.AddScoped<ISchemaMetadataStore, EfSchemaMetadataStore>();
        services.AddScoped<ISchemaReadStore, DapperSchemaReadStore>();

        // Phase 3: AI & PgVector Persistence Stores & Safe Execution
        services.AddScoped<IVectorStore, PgVectorEmbeddingStore>();
        services.AddScoped<ISafeQueryExecutor, Persistence.Execution.SafeQueryExecutor>();
        services.AddScoped<IQueryHistoryStore, EfQueryHistoryStore>();
        services.AddScoped<ISavedReportStore, EfSavedReportStore>();
        services.AddScoped<IConversationSessionStore, EfConversationSessionStore>();
        services.AddScoped<IPromptTemplateStore, EfPromptTemplateStore>();

        // Phase 5: Monitoring, Alerts & Executive Intelligence Stores
        services.AddScoped<IMonitorStore, EfMonitorStore>();
        services.AddScoped<IAlertStore, EfAlertStore>();
        services.AddScoped<IExecutiveReportStore, EfExecutiveReportStore>();
        services.AddSingleton<IAnomalyDetectionService, StatisticalAnomalyDetectionService>();

        // Phase A: Business Knowledge & Semantic Layer Stores
        services.AddScoped<IBusinessTermStore, EfBusinessTermStore>();
        services.AddScoped<IMetricStore, EfMetricStore>();

        // Phase B: Root-Cause Investigation Stores
        services.AddScoped<IInvestigationReportStore, EfInvestigationReportStore>();

        // Phase E: AI Custom Dashboard Stores
        services.AddScoped<ICustomDashboardStore, EfCustomDashboardStore>();

        // Phase F: Enterprise Data Governance Stores & Masking Engine
        services.AddScoped<IGovernancePolicyStore, EfGovernancePolicyStore>();
        services.AddSingleton<IDataMaskingEngine, DataMaskingEngine>();

        // Phase G: What-If Scenario Analysis Stores & Simulation Engine
        services.AddScoped<IScenarioStore, EfScenarioStore>();
        services.AddSingleton<IScenarioSimulationEngine, ScenarioSimulationEngine>();

        // Phase H: Recommendation Engine Stores
        services.AddScoped<IRecommendationStore, EfRecommendationStore>();

        // Phase I: Forecasting Foundation Stores & Statistical Engine
        services.AddScoped<IForecastStore, EfForecastStore>();
        services.AddSingleton<IForecastingEngine, ForecastingEngine>();

        // 6. Security, Hashing, Encryption & Tokens
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IEncryptionService, Security.AesEncryptionService>();

        // 7. Schema Discovery Engine
        services.AddSingleton<ISchemaDiscoveryEngine, SchemaDiscovery.SchemaDiscoveryEngine>();

        // 8. Quartz.NET Background Jobs & Distributed Scheduling
        services.AddQuartz(q =>
        {
            // 8.1 Schema Sync Job
            var schemaJobKey = new JobKey("SchemaSyncJob");
            q.AddJob<Background.SchemaSyncJob>(opts => opts.WithIdentity(schemaJobKey));
            q.AddTrigger(opts => opts
                .ForJob(schemaJobKey)
                .WithIdentity("SchemaSyncTrigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromHours(1)).RepeatForever()));

            // 8.2 Monitor Execution Job (Runs every 15 minutes)
            var monitorJobKey = new JobKey("MonitorExecutionJob");
            q.AddJob<Background.MonitorExecutionJob>(opts => opts.WithIdentity(monitorJobKey));
            q.AddTrigger(opts => opts
                .ForJob(monitorJobKey)
                .WithIdentity("MonitorExecutionTrigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(15)).RepeatForever()));

            // 8.3 Anomaly Detection Job (Runs every hour)
            var anomalyJobKey = new JobKey("AnomalyDetectionJob");
            q.AddJob<Background.AnomalyDetectionJob>(opts => opts.WithIdentity(anomalyJobKey));
            q.AddTrigger(opts => opts
                .ForJob(anomalyJobKey)
                .WithIdentity("AnomalyDetectionTrigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromHours(1)).RepeatForever()));

            // 8.4 Executive Report Job (Runs every 6 hours)
            var executiveJobKey = new JobKey("ExecutiveReportJob");
            q.AddJob<Background.ExecutiveReportJob>(opts => opts.WithIdentity(executiveJobKey));
            q.AddTrigger(opts => opts
                .ForJob(executiveJobKey)
                .WithIdentity("ExecutiveReportTrigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromHours(6)).RepeatForever()));

            // 8.5 Alert Generation & SLA Escalation Job (Runs every 4 hours)
            var alertJobKey = new JobKey("AlertGenerationJob");
            q.AddJob<Background.AlertGenerationJob>(opts => opts.WithIdentity(alertJobKey));
            q.AddTrigger(opts => opts
                .ForJob(alertJobKey)
                .WithIdentity("AlertGenerationTrigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromHours(4)).RepeatForever()));
        });
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        // 9. JWT Bearer Authentication
        var jwtSettings = new JwtOptions();
        configuration.GetSection(JwtOptions.SectionName).Bind(jwtSettings);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        // 10. Distributed Caching (Redis or in-memory fallback)
        //if (!string.IsNullOrWhiteSpace(redisConnection))
        //{
        //    services.AddStackExchangeRedisCache(options =>
        //    {
        //        options.Configuration = redisConnection;
        //        options.InstanceName = "IntelliQuery:";
        //    });
        //}
        //else
        //{
        //    services.AddDistributedMemoryCache();
        //}

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {

            var options = new ConfigurationOptions
            {
                EndPoints = { { configuration["RedisConnection:Host"]!, int.Parse(configuration["RedisConnection:Port"]!) } },
                User = configuration["RedisConnection:User"],
                Password = configuration["RedisConnection:Password"]
            };

            return ConnectionMultiplexer.Connect(options);
        });

        // 11. Infrastructure Services
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IEmailSender, EmailSender>();

        // 12. Paystack Payment Gateway
        services.Configure<PaystackOptions>(configuration.GetSection(PaystackOptions.SectionName));
        services.AddHttpClient<IPaystackService, PaystackService>();

        return services;
    }
}

