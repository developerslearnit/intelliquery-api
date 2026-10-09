using Azure.AI.OpenAI;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.AI.Embeddings;
using IntelliQueryAI.AI.Evaluations;
using IntelliQueryAI.AI.Memory;
using IntelliQueryAI.AI.Telemetry;
using IntelliQueryAI.AI.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace IntelliQueryAI.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentFramework(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Settings Setup
        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? string.Empty;
        var apiKey = configuration["AzureOpenAI:ApiKey"] ?? string.Empty;
        var deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o";
        var embeddingDeploymentName = configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small";

        float? temperature = null;
        if (float.TryParse(configuration["AzureOpenAI:Temperature"], out var parsedTemp))
        {
            temperature = parsedTemp;
        }

        int maxTokens = 2048;
        if (int.TryParse(configuration["AzureOpenAI:MaxTokens"], out var parsedTokens))
        {
            maxTokens = parsedTokens;
        }

        var settings = new AzureOpenAISettings
        {
            Endpoint = endpoint,
            ApiKey = apiKey,
            DeploymentName = deploymentName,
            EmbeddingDeploymentName = embeddingDeploymentName,
            MaxTokens = maxTokens,
            Temperature = temperature
        };

        services.AddSingleton(Options.Create(settings));

        // 2. Simulation Mode evaluation
        bool isSimulated = string.IsNullOrWhiteSpace(apiKey) ||
                           apiKey.Contains("YOUR_AZURE_OPENAI_API_KEY", StringComparison.OrdinalIgnoreCase) ||
                           string.IsNullOrWhiteSpace(endpoint) ||
                           endpoint.Contains("your-resource", StringComparison.OrdinalIgnoreCase);

        var simulationMode = new SimulationMode(isSimulated);
        services.AddSingleton(simulationMode);

        // 3. AzureOpenAIClient, IChatClient & Embedding Clients
        if (isSimulated)
        {
            services.AddSingleton<AzureOpenAIClient>(sp => null!);
            services.AddSingleton<IChatClient>(sp => null!);
            services.AddSingleton<ChatClient>(sp => null!);
            services.AddSingleton<EmbeddingClient>(sp => null!);
        }
        else
        {
            services.AddSingleton<AzureOpenAIClient>(sp =>
            {
                return new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));
            });

            services.AddSingleton<IChatClient>(sp =>
            {
                var client = sp.GetRequiredService<AzureOpenAIClient>();
                return client.GetChatClient(deploymentName).AsIChatClient();
            });

            services.AddSingleton<ChatClient>(sp =>
            {
                var client = sp.GetRequiredService<AzureOpenAIClient>();
                return client.GetChatClient(deploymentName);
            });

            services.AddSingleton<EmbeddingClient>(sp =>
            {
                var client = sp.GetRequiredService<AzureOpenAIClient>();
                return client.GetEmbeddingClient(embeddingDeploymentName);
            });
        }

        // 4. Infrastructure, Telemetry, Memory & Evaluations
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IAgentMemoryService, AgentMemoryService>();
        services.AddSingleton<IAgentEvaluationService, AgentEvaluationService>();

        // 5. Embedding Generator & Indexer
        services.AddScoped<IntelliQueryAI.AI.Embeddings.IEmbeddingGenerator, AzureOpenAIEmbeddingGenerator>(sp =>
        {
            var simMode = sp.GetRequiredService<SimulationMode>();
            var embeddingClient = simMode.IsEnabled ? null : sp.GetService<EmbeddingClient>();
            var logger = sp.GetRequiredService<ILogger<AzureOpenAIEmbeddingGenerator>>();
            return new AzureOpenAIEmbeddingGenerator(embeddingClient, simMode, logger);
        });
        services.AddScoped<ISchemaEmbeddingIndexer, SchemaEmbeddingIndexer>();

        // 6. Domain Tools
        services.AddScoped<SchemaRetrievalTools>();
        services.AddScoped<SqlExecutionTools>();

        // 7. Specialized Autonomous Agents
        services.AddScoped<SchemaDiscoveryAgent>(sp => new SchemaDiscoveryAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<SchemaDiscoveryAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        services.AddScoped<IntentUnderstandingAgent>(sp => new IntentUnderstandingAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<IntentUnderstandingAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        services.AddScoped<SqlGenerationAgent>(sp => new SqlGenerationAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<SqlGenerationAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        services.AddScoped<SqlValidationAgent>(sp => new SqlValidationAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<IAgentEvaluationService>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<SqlValidationAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        services.AddScoped<DataAnalystAgent>(sp => new DataAnalystAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<DataAnalystAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        services.AddScoped<VisualizationAgent>(sp => new VisualizationAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<VisualizationAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        // Phase 5 Autonomous Agents (Monitoring, Anomaly, Executive Briefing, Recommendations)
        services.AddScoped<MonitoringAgent>(sp => new MonitoringAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<MonitoringAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        services.AddScoped<AnomalyAgent>(sp => new AnomalyAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<AnomalyAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        services.AddScoped<ExecutiveSummaryAgent>(sp => new ExecutiveSummaryAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<ExecutiveSummaryAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        services.AddScoped<RecommendationAgent>(sp => new RecommendationAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<RecommendationAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        // Phase B Autonomous Investigation Agent
        services.AddScoped<RootCauseInvestigationAgent>(sp => new RootCauseInvestigationAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<RootCauseInvestigationAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        // Phase E AI Dashboard Generator Agent
        services.AddScoped<DashboardGeneratorAgent>(sp => new DashboardGeneratorAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<DashboardGeneratorAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        // Phase G Scenario Analyst Agent
        services.AddScoped<ScenarioAnalystAgent>(sp => new ScenarioAnalystAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<ScenarioAnalystAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        // Phase H Recommendation Analyst Agent
        services.AddScoped<RecommendationAnalystAgent>(sp => new RecommendationAnalystAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<RecommendationAnalystAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        // Phase I Forecast Analyst Agent
        services.AddScoped<ForecastAnalystAgent>(sp => new ForecastAnalystAgent(
            sp.GetService<IChatClient>(),
            sp.GetRequiredService<ITelemetryService>(),
            sp.GetRequiredService<ILogger<ForecastAnalystAgent>>(),
            sp.GetRequiredService<IOptions<AzureOpenAISettings>>(),
            sp.GetRequiredService<SimulationMode>()
        ));

        // 8. Multi-Agent Supervisors & Coordinators
        services.AddScoped<IAgentCoordinator, AgentCoordinator>();
        services.AddScoped<IExecutiveCoordinator, ExecutiveCoordinator>();
        services.AddScoped<IInvestigationCoordinator, InvestigationCoordinator>();
        services.AddScoped<IDashboardCoordinator, DashboardCoordinator>();
        services.AddScoped<IScenarioCoordinator, ScenarioCoordinator>();
        services.AddScoped<IRecommendationCoordinator, RecommendationCoordinator>();
        services.AddScoped<IForecastCoordinator, ForecastCoordinator>();

        return services;
    }
}


