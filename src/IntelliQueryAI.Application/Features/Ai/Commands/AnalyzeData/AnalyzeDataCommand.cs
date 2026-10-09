using System.Text.Json;
using FluentValidation;
using IntelliQueryAI.AI.Agents;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Ai.Dtos;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Ai.Commands.AnalyzeData;

public sealed record AnalyzeDataCommand(
    Guid WorkspaceId,
    Guid UserId,
    string Question,
    string DataJson) : ICommand<Result<AnalysisResponseDto>>;

public sealed class AnalyzeDataCommandValidator : AbstractValidator<AnalyzeDataCommand>
{
    public AnalyzeDataCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Question).NotEmpty();
        RuleFor(x => x.DataJson).NotEmpty();
    }
}

public sealed class AnalyzeDataCommandHandler : ICommandHandler<AnalyzeDataCommand, Result<AnalysisResponseDto>>
{
    private readonly DataAnalystAgent _dataAnalystAgent;
    private readonly VisualizationAgent _visualizationAgent;

    public AnalyzeDataCommandHandler(
        DataAnalystAgent dataAnalystAgent,
        VisualizationAgent visualizationAgent)
    {
        _dataAnalystAgent = dataAnalystAgent;
        _visualizationAgent = visualizationAgent;
    }

    public async Task<Result<AnalysisResponseDto>> HandleAsync(
        AnalyzeDataCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Invoke Data Analyst Agent
        string analysisPrompt = $"""
            Analytical Question: {command.Question}
            Data Rows to Analyze:
            {command.DataJson}
            """;

        var analysisRes = await _dataAnalystAgent.ExecuteAsync(new AgentRequest(analysisPrompt), cancellationToken);

        AnalysisOutput analysisOutput;
        try
        {
            analysisOutput = JsonSerializer.Deserialize<AnalysisOutput>(analysisRes.Content)
                ?? new AnalysisOutput(analysisRes.Content, [analysisRes.Content], [], []);
        }
        catch
        {
            analysisOutput = new AnalysisOutput(analysisRes.Content, [analysisRes.Content], [], []);
        }

        // 2. Invoke Visualization Agent
        string vizPrompt = $"""
            Question: {command.Question}
            Sample Data: {command.DataJson.Take(500)}
            """;

        var vizRes = await _visualizationAgent.ExecuteAsync(new AgentRequest(vizPrompt), cancellationToken);

        ChartDto? chartDto = null;
        try
        {
            var chartConfig = JsonSerializer.Deserialize<ChartConfiguration>(vizRes.Content);
            if (chartConfig is not null)
            {
                chartDto = new ChartDto(
                    chartConfig.RecommendedChart.ToString(),
                    chartConfig.Title,
                    chartConfig.XAxisColumn,
                    chartConfig.YAxisColumns,
                    chartConfig.Description,
                    chartConfig.StylingOptions);
            }
        }
        catch
        {
            // Fallback chart remains null
        }

        var dto = new AnalysisResponseDto(
            Summary: analysisOutput.Summary,
            KeyInsights: analysisOutput.KeyInsights,
            Outliers: analysisOutput.Outliers,
            Recommendations: analysisOutput.Recommendations,
            RecommendedChart: chartDto);

        return Result.Success(dto);
    }
}
