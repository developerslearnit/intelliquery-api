using System.Text.Json;
using FluentValidation;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Governance.Dtos;
using IntelliQueryAI.Domain.Entities.Audit;
using IntelliQueryAI.Domain.Entities.Governance;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Governance.Commands;

// 1. Create Governance Policy
public sealed record CreateGovernancePolicyCommand(
    Guid WorkspaceId,
    Guid UserId,
    string Name,
    string? Description = null,
    string TargetTable = "*",
    string TargetColumn = "*",
    string Classification = "Pii",
    string MaskingStrategy = "Partial",
    string? MaskingPattern = null,
    IReadOnlyList<string>? AllowedRoles = null,
    bool IsActive = true,
    bool IsAiMaskingEnforced = true) : ICommand<Result<GovernancePolicyDto>>;

public sealed class CreateGovernancePolicyCommandValidator : AbstractValidator<CreateGovernancePolicyCommand>
{
    public CreateGovernancePolicyCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetTable).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TargetColumn).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateGovernancePolicyCommandHandler : ICommandHandler<CreateGovernancePolicyCommand, Result<GovernancePolicyDto>>
{
    private readonly IGovernancePolicyStore _policyStore;
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public CreateGovernancePolicyCommandHandler(
        IGovernancePolicyStore policyStore,
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _policyStore = policyStore;
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<GovernancePolicyDto>> HandleAsync(
        CreateGovernancePolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.ContextTenantMismatch);
        }

        var classification = ParseClassification(command.Classification);
        var maskingStrategy = ParseMaskingStrategy(command.MaskingStrategy);
        var roles = command.AllowedRoles ?? new[] { "Owner", "Admin" };
        string allowedRolesJson = JsonSerializer.Serialize(roles);

        var policy = GovernancePolicy.Create(
            command.WorkspaceId,
            command.Name,
            command.Description,
            command.TargetTable,
            command.TargetColumn,
            classification,
            maskingStrategy,
            command.MaskingPattern,
            allowedRolesJson,
            command.IsActive,
            command.IsAiMaskingEnforced);

        await _policyStore.AddAsync(policy, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Governance.PolicyCreated",
            "GovernancePolicy",
            policy.Id.ToString(),
            $"Created data governance policy '{policy.Name}' targeting {policy.TargetTable}.{policy.TargetColumn} with {policy.MaskingStrategy} masking.",
            null,
            null), cancellationToken);

        return Result.Success(MapToDto(policy));
    }

    internal static GovernancePolicyDto MapToDto(GovernancePolicy p)
    {
        IReadOnlyList<string> roles = new[] { "Owner", "Admin" };
        try
        {
            roles = JsonSerializer.Deserialize<List<string>>(p.AllowedRolesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<string> { "Owner", "Admin" };
        }
        catch { }

        return new GovernancePolicyDto(
            p.Id,
            p.WorkspaceId,
            p.Name,
            p.Description,
            p.TargetTable,
            p.TargetColumn,
            p.Classification.ToString(),
            p.MaskingStrategy.ToString(),
            p.MaskingPattern,
            roles,
            p.IsActive,
            p.IsAiMaskingEnforced,
            p.CreatedAtUtc,
            p.UpdatedAtUtc);
    }

    internal static DataClassification ParseClassification(string? val)
    {
        if (Enum.TryParse<DataClassification>(val, true, out var parsed))
            return parsed;
        return DataClassification.Pii;
    }

    internal static MaskingStrategy ParseMaskingStrategy(string? val)
    {
        if (Enum.TryParse<MaskingStrategy>(val, true, out var parsed))
            return parsed;
        return MaskingStrategy.Partial;
    }
}

// 2. Update Governance Policy
public sealed record UpdateGovernancePolicyCommand(
    Guid WorkspaceId,
    Guid PolicyId,
    Guid UserId,
    string Name,
    string? Description = null,
    string TargetTable = "*",
    string TargetColumn = "*",
    string Classification = "Pii",
    string MaskingStrategy = "Partial",
    string? MaskingPattern = null,
    IReadOnlyList<string>? AllowedRoles = null,
    bool IsActive = true,
    bool IsAiMaskingEnforced = true) : ICommand<Result<GovernancePolicyDto>>;

public sealed class UpdateGovernancePolicyCommandHandler : ICommandHandler<UpdateGovernancePolicyCommand, Result<GovernancePolicyDto>>
{
    private readonly IGovernancePolicyStore _policyStore;
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public UpdateGovernancePolicyCommandHandler(
        IGovernancePolicyStore policyStore,
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _policyStore = policyStore;
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<GovernancePolicyDto>> HandleAsync(
        UpdateGovernancePolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.ContextTenantMismatch);
        }

        var policy = await _policyStore.GetByIdAsync(command.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.GovernancePolicyNotFound);
        }

        if (policy.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.GovernanceTenantMismatch);
        }

        var classification = CreateGovernancePolicyCommandHandler.ParseClassification(command.Classification);
        var maskingStrategy = CreateGovernancePolicyCommandHandler.ParseMaskingStrategy(command.MaskingStrategy);
        var roles = command.AllowedRoles ?? new[] { "Owner", "Admin" };
        string allowedRolesJson = JsonSerializer.Serialize(roles);

        policy.Update(
            command.Name,
            command.Description,
            command.TargetTable,
            command.TargetColumn,
            classification,
            maskingStrategy,
            command.MaskingPattern,
            allowedRolesJson,
            command.IsActive,
            command.IsAiMaskingEnforced);

        await _policyStore.UpdateAsync(policy, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Governance.PolicyUpdated",
            "GovernancePolicy",
            policy.Id.ToString(),
            $"Updated data governance policy '{policy.Name}' targeting {policy.TargetTable}.{policy.TargetColumn}.",
            null,
            null), cancellationToken);

        return Result.Success(CreateGovernancePolicyCommandHandler.MapToDto(policy));
    }
}

// 3. Toggle Governance Policy
public sealed record ToggleGovernancePolicyCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid PolicyId,
    bool? IsActive = null) : ICommand<Result<GovernancePolicyDto>>;

public sealed class ToggleGovernancePolicyCommandHandler : ICommandHandler<ToggleGovernancePolicyCommand, Result<GovernancePolicyDto>>
{
    private readonly IGovernancePolicyStore _policyStore;
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public ToggleGovernancePolicyCommandHandler(
        IGovernancePolicyStore policyStore,
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _policyStore = policyStore;
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<GovernancePolicyDto>> HandleAsync(
        ToggleGovernancePolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.ContextTenantMismatch);
        }

        var policy = await _policyStore.GetByIdAsync(command.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.GovernancePolicyNotFound);
        }

        if (policy.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<GovernancePolicyDto>(AiErrors.GovernanceTenantMismatch);
        }

        bool newActiveState = command.IsActive ?? !policy.IsActive;
        policy.ToggleActive(newActiveState);
        await _policyStore.UpdateAsync(policy, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Governance.TogglePolicy",
            "GovernancePolicy",
            policy.Id.ToString(),
            $"Toggled data governance policy '{policy.Name}' active state to {newActiveState}."), cancellationToken);

        return Result.Success(CreateGovernancePolicyCommandHandler.MapToDto(policy));
    }
}

// 4. Delete Governance Policy
public sealed record DeleteGovernancePolicyCommand(
    Guid WorkspaceId,
    Guid UserId,
    Guid PolicyId) : ICommand<Result>;

public sealed class DeleteGovernancePolicyCommandHandler : ICommandHandler<DeleteGovernancePolicyCommand, Result>
{
    private readonly IGovernancePolicyStore _policyStore;
    private readonly IAuditLogStore _auditStore;
    private readonly ITenantContext _tenantContext;

    public DeleteGovernancePolicyCommandHandler(
        IGovernancePolicyStore policyStore,
        IAuditLogStore auditStore,
        ITenantContext tenantContext)
    {
        _policyStore = policyStore;
        _auditStore = auditStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result> HandleAsync(
        DeleteGovernancePolicyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure(AiErrors.ContextTenantMismatch);
        }

        var policy = await _policyStore.GetByIdAsync(command.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Result.Failure(AiErrors.GovernancePolicyNotFound);
        }

        if (policy.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure(AiErrors.GovernanceTenantMismatch);
        }

        await _policyStore.DeleteAsync(policy, cancellationToken);

        await _auditStore.AddAsync(AuditLog.Create(
            command.WorkspaceId,
            command.UserId,
            "Governance.PolicyDeleted",
            "GovernancePolicy",
            policy.Id.ToString(),
            $"Deleted data governance policy '{policy.Name}'.",
            null,
            null), cancellationToken);

        return Result.Success();
    }
}
