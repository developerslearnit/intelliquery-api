using IntelliQueryAI.Application.Abstractions.Messaging;
using IntelliQueryAI.Application.Features.DataSources.Commands.SyncDataSource;
using IntelliQueryAI.Domain.Enums;
using IntelliQueryAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace IntelliQueryAI.Infrastructure.Background;

/// <summary>
/// Quartz.NET recurring background job that checks for active data sources requiring schema refresh.
/// </summary>
[DisallowConcurrentExecution]
public sealed class SchemaSyncJob : IJob
{
    private readonly IntelliQueryDbContext _context;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<SchemaSyncJob> _logger;

    public SchemaSyncJob(
        IntelliQueryDbContext context,
        IDispatcher dispatcher,
        ILogger<SchemaSyncJob> logger)
    {
        _context = context;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Quartz background schema sync job triggered at {UtcNow}", DateTime.UtcNow);

        // Find active data sources that haven't been synced in 24 hours or are still in Pending state
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var pendingDataSources = await _context.DataSources
            .Where(d => d.IsActive && d.SyncStatus != SyncStatus.InProgress && (d.LastSyncAt == null || d.LastSyncAt < cutoff))
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var ds in pendingDataSources)
        {
            try
            {
                _logger.LogInformation("Background syncing schema for data source {Id} ({Name})", ds.Id, ds.Name);
                var command = new SyncDataSourceCommand(ds.Id, ds.WorkspaceId);
                var result = await _dispatcher.SendAsync(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Background schema sync completed for data source {Id}: {Tables} tables discovered.", ds.Id, result.Value.TablesCount);
                }
                else
                {
                    _logger.LogWarning("Background schema sync failed for data source {Id}: {Error}", ds.Id, result.Error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during background sync of data source {Id}", ds.Id);
            }
        }
    }
}
