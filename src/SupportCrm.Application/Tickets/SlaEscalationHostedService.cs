namespace SupportCrm.Application.Tickets;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// This codebase's first recurring background job — a minimal stand-in for a real job
// scheduler (Hangfire/Quartz/etc.), per the story's explicit scope note. Runs escalation
// evaluation and SLA alerting/digests every EvaluationInterval, in one DI scope per tick
// (IServiceScopeFactory, since every dependency below it is Scoped).
public class SlaEscalationHostedService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    public static readonly TimeSpan EvaluationInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(EvaluationInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();

            var escalationEngine = scope.ServiceProvider.GetRequiredService<EscalationRuleEngine>();
            await escalationEngine.EvaluateAllAsync(stoppingToken);

            var alertService = scope.ServiceProvider.GetRequiredService<SlaAlertService>();
            await alertService.EvaluateAndSendAlertsAsync(stoppingToken);
            await alertService.SendDailyWeeklyDigestsAsync(stoppingToken);
        }
    }
}
