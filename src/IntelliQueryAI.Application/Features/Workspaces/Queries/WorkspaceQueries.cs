using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Workspaces.Dtos;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Workspaces.Queries;

/// <summary>
/// Query to retrieve a workspace by its unique tenant slug.
/// </summary>
public sealed record GetWorkspaceBySlugQuery(string Slug) : IQuery<Result<WorkspaceDto>>;

public sealed class GetWorkspaceBySlugQueryHandler : IQueryHandler<GetWorkspaceBySlugQuery, Result<WorkspaceDto>>
{
    private readonly IWorkspaceStore _workspaceStore;

    public GetWorkspaceBySlugQueryHandler(IWorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<WorkspaceDto>> HandleAsync(GetWorkspaceBySlugQuery query, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceStore.GetBySlugAsync(query.Slug, cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(WorkspaceErrors.NotFound);
        }

        return new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Slug.Value,
            workspace.SubscriptionPlan.ToString(),
            workspace.IsActive,
            workspace.CreatedAtUtc);
    }
}

/// <summary>
/// Query to retrieve a workspace by its unique identifier.
/// </summary>
public sealed record GetWorkspaceByIdQuery(Guid Id) : IQuery<Result<WorkspaceDto>>;

public sealed class GetWorkspaceByIdQueryHandler : IQueryHandler<GetWorkspaceByIdQuery, Result<WorkspaceDto>>
{
    private readonly IWorkspaceStore _workspaceStore;

    public GetWorkspaceByIdQueryHandler(IWorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<WorkspaceDto>> HandleAsync(GetWorkspaceByIdQuery query, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceStore.GetByIdAsync(query.Id, cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<WorkspaceDto>(WorkspaceErrors.NotFound);
        }

        return new WorkspaceDto(
            workspace.Id,
            workspace.Name,
            workspace.Slug.Value,
            workspace.SubscriptionPlan.ToString(),
            workspace.IsActive,
            workspace.CreatedAtUtc);
    }
}

/// <summary>
/// Query to retrieve all workspaces to which a given user belongs.
/// </summary>
public sealed record GetUserWorkspacesQuery(Guid UserId) : IQuery<Result<IReadOnlyList<WorkspaceDto>>>;

public sealed class GetUserWorkspacesQueryHandler : IQueryHandler<GetUserWorkspacesQuery, Result<IReadOnlyList<WorkspaceDto>>>
{
    private readonly IWorkspaceStore _workspaceStore;

    public GetUserWorkspacesQueryHandler(IWorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<IReadOnlyList<WorkspaceDto>>> HandleAsync(GetUserWorkspacesQuery query, CancellationToken cancellationToken = default)
    {
        var workspaces = await _workspaceStore.GetUserWorkspacesAsync(query.UserId, cancellationToken);
        bool hasPaidPlan = workspaces.Any(w => w.SubscriptionPlan is SubscriptionPlan.Pro or SubscriptionPlan.Enterprise);

        // Free tier clients only have access to 1 workspace
        var allowedWorkspaces = hasPaidPlan
            ? workspaces
            : workspaces.OrderBy(w => w.CreatedAtUtc).Take(1).ToList();

        var dtos = allowedWorkspaces
            .Select(w => new WorkspaceDto(
                w.Id,
                w.Name,
                w.Slug.Value,
                w.SubscriptionPlan.ToString(),
                w.IsActive,
                w.CreatedAtUtc))
            .ToList();

        return Result.Success<IReadOnlyList<WorkspaceDto>>(dtos);
    }
}
