namespace SupportCrm.Application.Integrations;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// INT-2's "on a defined schedule" — the same minimal PeriodicTimer stand-in for a real job
// scheduler as SlaEscalationHostedService, just for ERP sync instead of SLA evaluation.
public class ErpSyncHostedService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    public static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SyncInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ErpSyncService>();
            await syncService.SyncAllAsync(stoppingToken);
        }
    }
}
