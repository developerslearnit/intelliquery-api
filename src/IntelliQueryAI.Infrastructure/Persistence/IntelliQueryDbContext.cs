using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Entities.Alerts;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.DataSources;
using IntelliQueryAI.Domain.Entities.Executive;
using IntelliQueryAI.Domain.Entities.Monitoring;
using IntelliQueryAI.Domain.Entities.Subscriptions;
using IntelliQueryAI.Domain.Entities.Users;
using IntelliQueryAI.Domain.Entities.Workspaces;
using IntelliQueryAI.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Monitor = IntelliQueryAI.Domain.Entities.Monitoring.Monitor;

namespace IntelliQueryAI.Infrastructure.Persistence;

/// <summary>
/// Primary Entity Framework Core database context for the multi-tenant platform.
/// Note: By Clean Architecture design, this context is NOT injected into CQRS command or query handlers.
/// </summary>
public sealed class IntelliQueryDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DataSource> DataSources => Set<DataSource>();
    public DbSet<SchemaMetadata> SchemaMetadata => Set<SchemaMetadata>();
    public DbSet<TableRelationship> TableRelationships => Set<TableRelationship>();
    public DbSet<DatabaseRoutine> DatabaseRoutines => Set<DatabaseRoutine>();

    // Phase 3 AI & Vector Storage DbSets
    public DbSet<EmbeddingRecord> EmbeddingRecords => Set<EmbeddingRecord>();
    public DbSet<QueryHistory> QueryHistories => Set<QueryHistory>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();
    public DbSet<SavedReport> SavedReports => Set<SavedReport>();
    public DbSet<GeneratedInsight> GeneratedInsights => Set<GeneratedInsight>();

    // Phase 5 Monitoring, Alerts & Executive Intelligence DbSets
    public DbSet<Monitor> Monitors => Set<Monitor>();
    public DbSet<MonitorRule> MonitorRules => Set<MonitorRule>();
    public DbSet<MonitorExecution> MonitorExecutions => Set<MonitorExecution>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<ExecutiveReport> ExecutiveReports => Set<ExecutiveReport>();

    // Phase A Business Knowledge & Semantic Layer DbSets
    public DbSet<IntelliQueryAI.Domain.Entities.BusinessKnowledge.BusinessTerm> BusinessTerms => Set<IntelliQueryAI.Domain.Entities.BusinessKnowledge.BusinessTerm>();
    public DbSet<IntelliQueryAI.Domain.Entities.BusinessKnowledge.MetricDefinition> MetricDefinitions => Set<IntelliQueryAI.Domain.Entities.BusinessKnowledge.MetricDefinition>();

    // Phase B Root-Cause Investigation DbSets
    public DbSet<IntelliQueryAI.Domain.Entities.Investigations.InvestigationReport> InvestigationReports => Set<IntelliQueryAI.Domain.Entities.Investigations.InvestigationReport>();

    // Phase E AI Custom Dashboard DbSets
    public DbSet<IntelliQueryAI.Domain.Entities.Dashboards.CustomDashboard> CustomDashboards => Set<IntelliQueryAI.Domain.Entities.Dashboards.CustomDashboard>();

    // Phase F Enterprise Data Governance DbSets
    public DbSet<IntelliQueryAI.Domain.Entities.Governance.GovernancePolicy> GovernancePolicies => Set<IntelliQueryAI.Domain.Entities.Governance.GovernancePolicy>();

    // Phase G What-If Scenario Analysis DbSets
    public DbSet<IntelliQueryAI.Domain.Entities.Scenarios.Scenario> Scenarios => Set<IntelliQueryAI.Domain.Entities.Scenarios.Scenario>();

    // Phase H Recommendation Engine DbSets
    public DbSet<IntelliQueryAI.Domain.Entities.Recommendations.Recommendation> Recommendations => Set<IntelliQueryAI.Domain.Entities.Recommendations.Recommendation>();

    // Phase I Forecasting Foundation DbSets
    public DbSet<IntelliQueryAI.Domain.Entities.Forecasting.Forecast> Forecasts => Set<IntelliQueryAI.Domain.Entities.Forecasting.Forecast>();

    public IntelliQueryDbContext(
        DbContextOptions<IntelliQueryDbContext> options,
        ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Register PostgreSQL pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // Apply all configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntelliQueryDbContext).Assembly);
    }
}
