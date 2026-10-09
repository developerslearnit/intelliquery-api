using System.Text.Json;
using FluentValidation;
using IntelliQueryAI.AI.Coordinator;
using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.Dashboards.Dtos;
using IntelliQueryAI.Domain.Entities.Dashboards;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.Persistence;
using IntelliQueryAI.Domain.Services;
using IntelliQueryAI.SharedKernel.Results;

namespace IntelliQueryAI.Application.Features.Dashboards.Commands;

// 1. Generate Dashboard with AI Command
public sealed record GenerateDashboardWithAiCommand(
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string Prompt,
    Guid? DataSourceId = null,
    string? DateRange = "Last 30 Days") : ICommand<Result<CustomDashboardDto>>;

public sealed class GenerateDashboardWithAiCommandValidator : AbstractValidator<GenerateDashboardWithAiCommand>
{
    public GenerateDashboardWithAiCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(2000);
    }
}

public sealed class GenerateDashboardWithAiCommandHandler : ICommandHandler<GenerateDashboardWithAiCommand, Result<CustomDashboardDto>>
{
    private readonly IDashboardCoordinator _dashboardCoordinator;
    private readonly ITenantContext _tenantContext;
    private readonly IWorkspaceStore? _workspaceStore;

    public GenerateDashboardWithAiCommandHandler(
        IDashboardCoordinator dashboardCoordinator,
        ITenantContext tenantContext,
        IWorkspaceStore? workspaceStore = null)
    {
        _dashboardCoordinator = dashboardCoordinator;
        _tenantContext = tenantContext;
        _workspaceStore = workspaceStore;
    }

    public async Task<Result<CustomDashboardDto>> HandleAsync(
        GenerateDashboardWithAiCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.ContextTenantMismatch);
        }

        string? workspaceName = null;
        if (_workspaceStore is not null)
        {
            var ws = await _workspaceStore.GetByIdAsync(command.WorkspaceId, cancellationToken);
            workspaceName = ws?.Name;
        }

        var req = new GenerateDashboardPromptRequest(
            command.WorkspaceId,
            command.Prompt,
            command.DataSourceId,
            command.DateRange,
            workspaceName);

        var coordResult = await _dashboardCoordinator.GenerateDashboardDefinitionAsync(req, cancellationToken);
        if (!coordResult.IsSuccess)
        {
            return Result.Failure<CustomDashboardDto>(coordResult.Error);
        }

        var agentResult = coordResult.Value;

        var widgetDtos = agentResult.Widgets.Select(w => new DashboardWidgetDto(
            Id: Guid.NewGuid().ToString(),
            Title: w.Title,
            WidgetType: DashboardWidgetValidator.NormalizeWidgetType(w.WidgetType),
            Metric: w.Metric,
            Dimension: w.Dimension,
            Aggregation: w.Aggregation ?? "SUM",
            SqlQuery: w.SqlQuery,
            DateRange: w.DateRange ?? agentResult.DateRange,
            ColSpan: w.ColSpan > 0 ? w.ColSpan : 1,
            RowSpan: w.RowSpan > 0 ? w.RowSpan : 1,
            AdditionalOptionsJson: w.Options
        )).ToList();

        var dto = new CustomDashboardDto(
            Id: Guid.NewGuid(),
            WorkspaceId: command.WorkspaceId,
            CreatedByUserId: command.CreatedByUserId,
            Title: agentResult.Title,
            Description: agentResult.Description,
            Prompt: command.Prompt,
            DateRange: agentResult.DateRange,
            IsDefault: false,
            IsShared: true,
            Widgets: widgetDtos,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

        return Result.Success(dto);
    }
}

// 2. Create Custom Dashboard Command
public sealed record CreateCustomDashboardCommand(
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string Title,
    string? Description = null,
    string? Prompt = null,
    string? DateRange = "Last 30 Days",
    IReadOnlyList<DashboardWidgetDto>? Widgets = null,
    bool IsDefault = false,
    bool IsShared = true) : ICommand<Result<CustomDashboardDto>>;

public sealed class CreateCustomDashboardCommandValidator : AbstractValidator<CreateCustomDashboardCommand>
{
    public CreateCustomDashboardCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateCustomDashboardCommandHandler : ICommandHandler<CreateCustomDashboardCommand, Result<CustomDashboardDto>>
{
    private readonly ICustomDashboardStore _dashboardStore;
    private readonly ITenantContext _tenantContext;

    public CreateCustomDashboardCommandHandler(
        ICustomDashboardStore dashboardStore,
        ITenantContext tenantContext)
    {
        _dashboardStore = dashboardStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<CustomDashboardDto>> HandleAsync(
        CreateCustomDashboardCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.ContextTenantMismatch);
        }

        var widgets = command.Widgets ?? Array.Empty<DashboardWidgetDto>();

        // Validate all widget types
        foreach (var w in widgets)
        {
            if (!DashboardWidgetValidator.IsValidWidgetType(w.WidgetType))
            {
                return Result.Failure<CustomDashboardDto>(AiErrors.UnsupportedWidgetType(w.WidgetType));
            }
        }

        string layoutJson = JsonSerializer.Serialize(widgets);

        var dashboard = CustomDashboard.Create(
            command.WorkspaceId,
            command.CreatedByUserId,
            command.Title,
            command.Description,
            command.Prompt,
            layoutJson,
            command.DateRange ?? "Last 30 Days",
            command.IsDefault,
            command.IsShared);

        await _dashboardStore.AddAsync(dashboard, cancellationToken);

        return Result.Success(new CustomDashboardDto(
            dashboard.Id,
            dashboard.WorkspaceId,
            dashboard.CreatedByUserId,
            dashboard.Title,
            dashboard.Description,
            dashboard.Prompt,
            dashboard.DateRange,
            dashboard.IsDefault,
            dashboard.IsShared,
            widgets,
            dashboard.CreatedAtUtc,
            dashboard.UpdatedAtUtc));
    }
}

// 3. Update Custom Dashboard Command
public sealed record UpdateCustomDashboardCommand(
    Guid WorkspaceId,
    Guid DashboardId,
    string Title,
    string? Description = null,
    string? DateRange = "Last 30 Days",
    IReadOnlyList<DashboardWidgetDto>? Widgets = null,
    bool IsDefault = false,
    bool IsShared = true) : ICommand<Result<CustomDashboardDto>>;

public sealed class UpdateCustomDashboardCommandHandler : ICommandHandler<UpdateCustomDashboardCommand, Result<CustomDashboardDto>>
{
    private readonly ICustomDashboardStore _dashboardStore;
    private readonly ITenantContext _tenantContext;

    public UpdateCustomDashboardCommandHandler(
        ICustomDashboardStore dashboardStore,
        ITenantContext tenantContext)
    {
        _dashboardStore = dashboardStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<CustomDashboardDto>> HandleAsync(
        UpdateCustomDashboardCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.ContextTenantMismatch);
        }

        var dashboard = await _dashboardStore.GetByIdAsync(command.DashboardId, cancellationToken);
        if (dashboard is null)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.DashboardNotFound);
        }

        if (dashboard.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.DashboardTenantMismatch);
        }

        var widgets = command.Widgets ?? Array.Empty<DashboardWidgetDto>();
        foreach (var w in widgets)
        {
            if (!DashboardWidgetValidator.IsValidWidgetType(w.WidgetType))
            {
                return Result.Failure<CustomDashboardDto>(AiErrors.UnsupportedWidgetType(w.WidgetType));
            }
        }

        string layoutJson = JsonSerializer.Serialize(widgets);

        dashboard.Update(
            command.Title,
            command.Description,
            layoutJson,
            command.DateRange ?? "Last 30 Days",
            command.IsDefault,
            command.IsShared);

        await _dashboardStore.UpdateAsync(dashboard, cancellationToken);

        return Result.Success(new CustomDashboardDto(
            dashboard.Id,
            dashboard.WorkspaceId,
            dashboard.CreatedByUserId,
            dashboard.Title,
            dashboard.Description,
            dashboard.Prompt,
            dashboard.DateRange,
            dashboard.IsDefault,
            dashboard.IsShared,
            widgets,
            dashboard.CreatedAtUtc,
            dashboard.UpdatedAtUtc));
    }
}

// 4. Duplicate Custom Dashboard Command
public sealed record DuplicateCustomDashboardCommand(
    Guid WorkspaceId,
    Guid DashboardId,
    Guid CreatedByUserId) : ICommand<Result<CustomDashboardDto>>;

public sealed class DuplicateCustomDashboardCommandHandler : ICommandHandler<DuplicateCustomDashboardCommand, Result<CustomDashboardDto>>
{
    private readonly ICustomDashboardStore _dashboardStore;
    private readonly ITenantContext _tenantContext;

    public DuplicateCustomDashboardCommandHandler(
        ICustomDashboardStore dashboardStore,
        ITenantContext tenantContext)
    {
        _dashboardStore = dashboardStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<CustomDashboardDto>> HandleAsync(
        DuplicateCustomDashboardCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.ContextTenantMismatch);
        }

        var original = await _dashboardStore.GetByIdAsync(command.DashboardId, cancellationToken);
        if (original is null)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.DashboardNotFound);
        }

        if (original.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure<CustomDashboardDto>(AiErrors.DashboardTenantMismatch);
        }

        var duplicate = CustomDashboard.Create(
            command.WorkspaceId,
            command.CreatedByUserId,
            $"Copy of {original.Title}",
            original.Description,
            original.Prompt,
            original.LayoutJson,
            original.DateRange,
            isDefault: false,
            isShared: original.IsShared);

        await _dashboardStore.AddAsync(duplicate, cancellationToken);

        IReadOnlyList<DashboardWidgetDto> widgets = Array.Empty<DashboardWidgetDto>();
        try
        {
            widgets = JsonSerializer.Deserialize<List<DashboardWidgetDto>>(duplicate.LayoutJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<DashboardWidgetDto>();
        }
        catch { }

        return Result.Success(new CustomDashboardDto(
            duplicate.Id,
            duplicate.WorkspaceId,
            duplicate.CreatedByUserId,
            duplicate.Title,
            duplicate.Description,
            duplicate.Prompt,
            duplicate.DateRange,
            duplicate.IsDefault,
            duplicate.IsShared,
            widgets,
            duplicate.CreatedAtUtc,
            duplicate.UpdatedAtUtc));
    }
}

// 5. Delete Custom Dashboard Command
public sealed record DeleteCustomDashboardCommand(
    Guid WorkspaceId,
    Guid DashboardId) : ICommand<Result>;

public sealed class DeleteCustomDashboardCommandHandler : ICommandHandler<DeleteCustomDashboardCommand, Result>
{
    private readonly ICustomDashboardStore _dashboardStore;
    private readonly ITenantContext _tenantContext;

    public DeleteCustomDashboardCommandHandler(
        ICustomDashboardStore dashboardStore,
        ITenantContext tenantContext)
    {
        _dashboardStore = dashboardStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result> HandleAsync(
        DeleteCustomDashboardCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_tenantContext.WorkspaceId.HasValue && _tenantContext.WorkspaceId.Value != command.WorkspaceId)
        {
            return Result.Failure(AiErrors.ContextTenantMismatch);
        }

        var dashboard = await _dashboardStore.GetByIdAsync(command.DashboardId, cancellationToken);
        if (dashboard is null)
        {
            return Result.Failure(AiErrors.DashboardNotFound);
        }

        if (dashboard.WorkspaceId != command.WorkspaceId)
        {
            return Result.Failure(AiErrors.DashboardTenantMismatch);
        }

        await _dashboardStore.DeleteAsync(dashboard, cancellationToken);
        return Result.Success();
    }
}
