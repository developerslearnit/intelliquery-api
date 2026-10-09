using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Governance.Commands;
using IntelliQueryAI.Application.Features.Governance.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Governance.Queries;

// 1. Get All Governance Policies
public sealed record GetGovernancePoliciesQuery(
    Guid WorkspaceId,
    bool ActiveOnly = false) : IQuery<Result<IReadOnlyList<GovernancePolicyDto>>>;

public sealed class GetGovernancePoliciesQueryHandler : IQueryHandler<GetGovernancePoliciesQuery, Result<IReadOnlyList<GovernancePolicyDto>>>
{
    private readonly IGovernancePolicyStore _policyStore;
    private readonly ITenantContext _tenantContext;

    public GetGovernancePoliciesQueryHandler(
        IGovernancePolicyStore policyStore,
        ITenantContext tenantContext)
    {
        _policyStore = policyStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<GovernancePolicyDto>>> HandleAsync(
        GetGovernancePoliciesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<IReadOnlyList<GovernancePolicyDto>>(AiErrors.ContextTenantMismatch);
        }

        var policies = await _policyStore.GetByWorkspaceIdAsync(query.WorkspaceId, query.ActiveOnly, cancellationToken);
        var dtos = policies.Select(CreateGovernancePolicyCommandHandler.MapToDto).ToList();

        return Result.Success<IReadOnlyList<GovernancePolicyDto>>(dtos);
    }
}

// 2. Get Governance Policy By Id
public sealed record GetGovernancePolicyByIdQuery(
    Guid WorkspaceId,
    Guid PolicyId) : IQuery<Result<GovernancePolicyDto>>;

public sealed class GetGovernancePolicyByIdQueryHandler : IQueryHandler<GetGovernancePolicyByIdQuery, Result<GovernancePolicyDto>>
{
    private readonly IGovernancePolicyStore _policyStore;
    private readonly ITenantContext _tenantContext;

    public GetGovernancePolicyByIdQueryHandler(
        IGovernancePolicyStore policyStore,
        ITenantContext tenantContext)
    {
        _policyStore = policyStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<GovernancePolicyDto>> HandleAsync(
        GetGovernancePolicyByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.ContextTenantMismatch);
        }

        var policy = await _policyStore.GetByIdAsync(query.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.GovernancePolicyNotFound);
        }

        if (policy.WorkspaceId != query.WorkspaceId)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.GovernanceTenantMismatch);
        }

        return Result.Success(CreateGovernancePolicyCommandHandler.MapToDto(policy));
    }
}

// 3. Test Masking Preview (Playground)
public sealed record TestMaskingPreviewQuery(
    string SampleValue,
    string MaskingStrategy = "Partial",
    string? MaskingPattern = null) : IQuery<Result<TestMaskingPreviewResponseDto>>;

public sealed class TestMaskingPreviewQueryHandler : IQueryHandler<TestMaskingPreviewQuery, Result<TestMaskingPreviewResponseDto>>
{
    private readonly IDataMaskingEngine _maskingEngine;

    public TestMaskingPreviewQueryHandler(IDataMaskingEngine maskingEngine)
    {
        _maskingEngine = maskingEngine;
    }

    public Task<Result<TestMaskingPreviewResponseDto>> HandleAsync(
        TestMaskingPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        var strategy = CreateGovernancePolicyCommandHandler.ParseMaskingStrategy(query.MaskingStrategy);
        string masked = _maskingEngine.TestMask(query.SampleValue, strategy, query.MaskingPattern);

        var response = new TestMaskingPreviewResponseDto(
            OriginalValue: query.SampleValue,
            MaskedValue: masked,
            MaskingStrategy: strategy.ToString());

        return Task.FromResult(Result.Success(response));
    }
}

// 4. Get Governance Sensitive Access Audit Logs
public sealed record GetGovernanceAuditLogsQuery(
    Guid WorkspaceId,
    int Limit = 50) : IQuery<Result<IReadOnlyList<GovernanceAuditLogDto>>>;

public sealed class GetGovernanceAuditLogsQueryHandler : IQueryHandler<GetGovernanceAuditLogsQuery, Result<IReadOnlyList<GovernanceAuditLogDto>>>
{
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public GetGovernanceAuditLogsQueryHandler(
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<GovernanceAuditLogDto>>> HandleAsync(
        GetGovernanceAuditLogsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != query.WorkspaceId)
        {
            return Result.Failure<IReadOnlyList<GovernanceAuditLogDto>>(AiErrors.ContextTenantMismatch);
        }

        var allLogs = await _auditStore.GetByWorkspaceAsync(query.WorkspaceId, 100, cancellationToken);

        var govLogs = allLogs
            .Where(l => l.Action.StartsWith("Governance", StringComparison.OrdinalIgnoreCase) ||
                        l.Action.StartsWith("DataGovernance", StringComparison.OrdinalIgnoreCase))
            .Take(query.Limit)
            .Select(l => new GovernanceAuditLogDto(
                l.Id,
                l.UserId,
                l.Action,
                l.EntityType,
                l.EntityId,
                l.Details,
                l.TimestampUtc))
            .ToList();

        return Result.Success<IReadOnlyList<GovernanceAuditLogDto>>(govLogs);
    }
}
