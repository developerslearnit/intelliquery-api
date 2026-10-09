using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Ai.Dtos;
using IntelliQueryAI.Domain.Entities.Ai;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Ai.Commands.SaveReport;

public sealed record SaveReportCommand(
    Guid WorkspaceId,
    Guid UserId,
    string Title,
    string? Description,
    string Question,
    string SqlQuery,
    string ChartConfigurationJson,
    string InsightsJson) : ICommand<Result<SavedReportDto>>;

public sealed class SaveReportCommandValidator : AbstractValidator<SaveReportCommand>
{
    public SaveReportCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Question).NotEmpty();
        RuleFor(x => x.SqlQuery).NotEmpty();
    }
}

public sealed class SaveReportCommandHandler : ICommandHandler<SaveReportCommand, Result<SavedReportDto>>
{
    private readonly ISavedReportStore _reportStore;

    public SaveReportCommandHandler(ISavedReportStore reportStore)
    {
        _reportStore = reportStore;
    }

    public async Task<Result<SavedReportDto>> HandleAsync(
        SaveReportCommand command,
        CancellationToken cancellationToken = default)
    {
        string chartJson = !string.IsNullOrWhiteSpace(command.ChartConfigurationJson)
            ? command.ChartConfigurationJson
            : "{}";

        string insightsJson = !string.IsNullOrWhiteSpace(command.InsightsJson)
            ? command.InsightsJson
            : "[]";

        var report = SavedReport.Create(
            command.WorkspaceId,
            command.UserId,
            command.Title,
            command.Description,
            command.Question,
            command.SqlQuery,
            chartJson,
            insightsJson);

        await _reportStore.AddAsync(report, cancellationToken);

        var dto = new SavedReportDto(
            report.Id,
            report.WorkspaceId,
            report.UserId,
            report.Title,
            report.Description,
            report.Question,
            report.SqlQuery,
            report.ChartConfigurationJson,
            report.InsightsJson,
            report.CreatedAtUtc,
            report.SqlQuery,
            ExtractChartType(report.ChartConfigurationJson),
            report.CreatedAtUtc);

        return Result.Success(dto);
    }

    private static string? ExtractChartType(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("chartType", out var prop))
                return prop.GetString();
            if (doc.RootElement.TryGetProperty("ChartType", out var propUpper))
                return propUpper.GetString();
        }
        catch { }
        return null;
    }
}
