using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Infrastructure.Configuration;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class MetaCatalogSyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MetaCatalogOptions> options,
    ILogger<MetaCatalogSyncWorker> logger) : BackgroundService
{
    private readonly MetaCatalogOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IMetaCatalogSyncService>();
                await syncService.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Meta catalog sync worker failed while processing pending items.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_options.WorkerIntervalSeconds, 10, 3600)), stoppingToken);
        }
    }
}
