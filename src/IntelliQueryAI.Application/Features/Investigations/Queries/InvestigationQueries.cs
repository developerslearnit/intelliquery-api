using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Investigations.Dtos;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Investigations.Queries;

// ============================================================================
// 1. Get Investigations List
// ============================================================================

public sealed record GetInvestigationsQuery(
    Guid? WorkspaceId = null,
    int Limit = 50
) : IQuery<Result<IReadOnlyList<InvestigationReportDto>>>;

public sealed class GetInvestigationsQueryHandler : IQueryHandler<GetInvestigationsQuery, Result<IReadOnlyList<InvestigationReportDto>>>
{
    private readonly IInvestigationReportStore _investigationReportStore;
    private readonly ITenantContext _tenantContext;

    public GetInvestigationsQueryHandler(
        IInvestigationReportStore investigationReportStore,
        ITenantContext tenantContext)
    {
        _investigationReportStore = investigationReportStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<InvestigationReportDto>>> HandleAsync(
        GetInvestigationsQuery query,
        CancellationToken cancellationToken)
    {
        var targetWorkspaceId = query.WorkspaceId ?? _tenantContext.WorkspaceId;
        if (!targetWorkspaceId.HasValue || targetWorkspaceId.Value == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<InvestigationReportDto>>(
                Error.Unauthorized("Tenant.Missing", "A valid workspace context is required."));
        }

        var reports = await _investigationReportStore.GetByWorkspaceIdAsync(
            targetWorkspaceId.Value, query.Limit, cancellationToken);

        var dtos = reports.Select(InvestigationReportDto.FromDomain).ToList();
        return Result.Success<IReadOnlyList<InvestigationReportDto>>(dtos);
    }
}

// ============================================================================
// 2. Get Investigation By Id
// ============================================================================

public sealed record GetInvestigationByIdQuery(
    Guid Id,
    Guid? WorkspaceId = null
) : IQuery<Result<InvestigationReportDto>>;

public sealed class GetInvestigationByIdQueryHandler : IQueryHandler<GetInvestigationByIdQuery, Result<InvestigationReportDto>>
{
    private readonly IInvestigationReportStore _investigationReportStore;
    private readonly ITenantContext _tenantContext;

    public GetInvestigationByIdQueryHandler(
        IInvestigationReportStore investigationReportStore,
        ITenantContext tenantContext)
    {
        _investigationReportStore = investigationReportStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<InvestigationReportDto>> HandleAsync(
        GetInvestigationByIdQuery query,
        CancellationToken cancellationToken)
    {
        var targetWorkspaceId = query.WorkspaceId ?? _tenantContext.WorkspaceId;
        var report = await _investigationReportStore.GetByIdAsync(query.Id, cancellationToken);

        if (report == null || (targetWorkspaceId.HasValue && report.WorkspaceId != targetWorkspaceId.Value))
        {
            return Result.Failure<InvestigationReportDto>(
                Error.NotFound("Investigation.NotFound", "Investigation report not found."));
        }

        return Result.Success(InvestigationReportDto.FromDomain(report));
    }
}
