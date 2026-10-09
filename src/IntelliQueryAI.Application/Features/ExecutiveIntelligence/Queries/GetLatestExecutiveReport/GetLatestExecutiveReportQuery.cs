using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Dtos;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReports;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetLatestExecutiveReport;

public sealed record GetLatestExecutiveReportQuery(Guid WorkspaceId) : IQuery<Result<ExecutiveReportDto>>;

public sealed class GetLatestExecutiveReportQueryHandler : IQueryHandler<GetLatestExecutiveReportQuery, Result<ExecutiveReportDto>>
{
    private readonly IExecutiveReportStore _store;

    public GetLatestExecutiveReportQueryHandler(IExecutiveReportStore store)
    {
        _store = store;
    }

    public async Task<Result<ExecutiveReportDto>> HandleAsync(
        GetLatestExecutiveReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var report = await _store.GetLatestByWorkspaceIdAsync(query.WorkspaceId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<ExecutiveReportDto>(MonitoringErrors.ExecutiveReportNotFound);
        }

        return Result.Success(GetExecutiveReportsQueryHandler.MapToDto(report));
    }
}
