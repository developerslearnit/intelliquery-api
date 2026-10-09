using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IntelliQueryAI.AI.Telemetry;

public interface ITelemetryService
{
    void TrackAgentExecution(string agentName, long durationMs, int tokenCount, bool isSuccess = true);
    void TrackToolInvocation(string toolName, long durationMs, bool isSuccess = true);
    AgentTelemetrySummary GetSummary();
}

public sealed record AgentTelemetrySummary(
    long TotalExecutions,
    long TotalDurationMs,
    long TotalTokens,
    IReadOnlyDictionary<string, long> ExecutionsByAgent,
    IReadOnlyDictionary<string, long> TokensByAgent);

public sealed class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;
    private long _totalExecutions;
    private long _totalDurationMs;
    private long _totalTokens;
    private readonly ConcurrentDictionary<string, long> _agentExecutions = new();
    private readonly ConcurrentDictionary<string, long> _agentTokens = new();

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
    }

    public void TrackAgentExecution(string agentName, long durationMs, int tokenCount, bool isSuccess = true)
    {
        Interlocked.Increment(ref _totalExecutions);
        Interlocked.Add(ref _totalDurationMs, durationMs);
        Interlocked.Add(ref _totalTokens, tokenCount);

        _agentExecutions.AddOrUpdate(agentName, 1, (_, count) => count + 1);
        _agentTokens.AddOrUpdate(agentName, tokenCount, (_, tokens) => tokens + tokenCount);

        _logger.LogInformation(
            "[AI Telemetry] Agent '{AgentName}' completed in {DurationMs}ms using {Tokens} tokens. Success={Success}",
            agentName, durationMs, tokenCount, isSuccess);
    }

    public void TrackToolInvocation(string toolName, long durationMs, bool isSuccess = true)
    {
        _logger.LogDebug(
            "[AI Telemetry] Tool '{ToolName}' executed in {DurationMs}ms. Success={Success}",
            toolName, durationMs, isSuccess);
    }

    public AgentTelemetrySummary GetSummary()
    {
        return new AgentTelemetrySummary(
            Interlocked.Read(ref _totalExecutions),
            Interlocked.Read(ref _totalDurationMs),
            Interlocked.Read(ref _totalTokens),
            _agentExecutions.ToDictionary(k => k.Key, v => v.Value),
            _agentTokens.ToDictionary(k => k.Key, v => v.Value));
    }
}
