using System.Diagnostics;
using IntelliQueryAI.AI.Common;
using IntelliQueryAI.AI.Telemetry;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Agents;

public sealed record AgentRequest(
    string Prompt,
    string? ConversationId = null,
    string? UserId = null,
    IReadOnlyDictionary<string, object>? Context = null
);

public sealed record AgentResponse(
    string Content,
    string AgentName,
    bool IsSuccess = true,
    int TotalTokens = 0,
    long DurationMs = 0,
    IReadOnlyDictionary<string, object>? Metadata = null
)
{
    public static AgentResponse Success(
        string content,
        string agentName,
        int totalTokens = 0,
        long durationMs = 0,
        IReadOnlyDictionary<string, object>? metadata = null) =>
        new(content, agentName, true, totalTokens, durationMs, metadata);

    public static AgentResponse Failure(
        string errorMessage,
        string agentName,
        long durationMs = 0) =>
        new(errorMessage, agentName, false, 0, durationMs);
}

public interface IBaseAgent
{
    string Name { get; }
    string Description { get; }
    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken cancellationToken = default);
}

public abstract class BaseAgent : IBaseAgent
{
    private readonly IChatClient? _chatClient;
    private readonly ITelemetryService _telemetry;
    private readonly ILogger _logger;
    private readonly SimulationMode _simulationMode;
    private readonly List<AITool> _tools = [];
    private readonly float? _temperature;

    public string Name { get; }
    public string Description { get; }
    public string Instructions { get; }
    public IReadOnlyList<AITool> Tools => _tools.AsReadOnly();

    protected BaseAgent(
        IChatClient? chatClient,
        string instructions,
        string name,
        string description,
        IEnumerable<AITool>? tools,
        ITelemetryService telemetry,
        ILogger logger,
        SimulationMode simulationMode,
        float? temperature = null)
    {
        _chatClient = chatClient;
        Instructions = instructions;
        Name = name;
        Description = description;
        _telemetry = telemetry;
        _logger = logger;
        _simulationMode = simulationMode;
        _temperature = temperature;

        if (tools is not null)
        {
            _tools.AddRange(tools);
        }
    }

    public virtual async Task<AgentResponse> ExecuteAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Agent '{AgentName}' executing prompt: {Prompt}", Name, request.Prompt);

        // Simulation / Offline Mode Fallback
        if (_simulationMode.IsEnabled || _chatClient is null)
        {
            _logger.LogWarning("Agent '{AgentName}' executing in SIMULATION mode.", Name);
            await Task.Delay(100, cancellationToken); // Realistic inference latency
            stopwatch.Stop();

            var simulatedText = GenerateSimulatedResponse(request);
            _telemetry.TrackAgentExecution(Name, stopwatch.ElapsedMilliseconds, 0);

            return AgentResponse.Success(simulatedText, Name, 0, stopwatch.ElapsedMilliseconds);
        }

        try
        {
            var options = new ChatOptions();
            if (_temperature.HasValue)
            {
                options.Temperature = _temperature.Value;
            }

            if (_tools.Count > 0)
            {
                options.Tools = _tools;
            }

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, Instructions),
                new(ChatRole.User, request.Prompt)
            };

            ChatResponse response;
            try
            {
                response = await _chatClient.GetResponseAsync(messages, options, cancellationToken);
            }
            catch (Exception ex) when (options.Temperature.HasValue &&
                                       ex.Message.Contains("temperature", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Agent '{AgentName}': Model does not support custom temperature '{Temp}'. Retrying with default model temperature.",
                    Name, options.Temperature);
                options.Temperature = null;
                response = await _chatClient.GetResponseAsync(messages, options, cancellationToken);
            }

            stopwatch.Stop();

            int tokens = (int)(response.Usage?.TotalTokenCount ?? 0);
            _telemetry.TrackAgentExecution(Name, stopwatch.ElapsedMilliseconds, tokens);

            string responseText = response.Text ?? string.Empty;

            return AgentResponse.Success(
                content: responseText,
                agentName: Name,
                totalTokens: tokens,
                durationMs: stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error occurred during execution of agent '{AgentName}'", Name);
            return AgentResponse.Failure(ex.Message, Name, stopwatch.ElapsedMilliseconds);
        }
    }

    protected abstract string GenerateSimulatedResponse(AgentRequest request);
}
