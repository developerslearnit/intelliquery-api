namespace IntelliQueryAI.AI.Common;

/// <summary>
/// Configuration options for Azure OpenAI and embedding deployments.
/// </summary>
public sealed class AzureOpenAISettings
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-3-small";
    public int MaxTokens { get; set; } = 2048;
    public float? Temperature { get; set; } = null;
}

/// <summary>
/// Flag indicating whether agents execute in offline simulation mode.
/// Allows deterministic testing and local development without active Azure OpenAI credentials.
/// </summary>
public sealed class SimulationMode
{
    public bool IsEnabled { get; }

    public SimulationMode(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
