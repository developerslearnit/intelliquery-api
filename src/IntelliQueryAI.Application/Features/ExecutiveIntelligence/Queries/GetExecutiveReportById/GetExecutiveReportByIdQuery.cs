using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Dtos;
using IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReports;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.ExecutiveIntelligence.Queries.GetExecutiveReportById;

public sealed record GetExecutiveReportByIdQuery(Guid WorkspaceId, Guid ReportId) : IQuery<Result<ExecutiveReportDto>>;

public sealed class GetExecutiveReportByIdQueryHandler : IQueryHandler<GetExecutiveReportByIdQuery, Result<ExecutiveReportDto>>
{
    private readonly IExecutiveReportStore _store;
    private readonly IWorkspaceStore? _workspaceStore;

    public GetExecutiveReportByIdQueryHandler(
        IExecutiveReportStore store,
        IWorkspaceStore? workspaceStore = null)
    {
        _store = store;
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<ExecutiveReportDto>> HandleAsync(
        GetExecutiveReportByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var report = await _store.GetByIdAsync(query.ReportId, cancellationToken);
        if (report == null || report.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<ExecutiveReportDto>(Error.NotFound("ExecutiveReport.NotFound", "The executive report was not found."));
        }

        string? workspaceName = null;
        if (_workspaceStore is not null)
        {
            var ws = await _workspaceStore.GetByIdAsync(query.WorkspaceId, cancellationToken);
            workspaceName = ws?.Name;
        }

        return Result.Success(GetExecutiveReportsQueryHandler.MapToDto(report, workspaceName));
    }
}
