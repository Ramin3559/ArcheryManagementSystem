using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EShooting.Web.BackgroundServices;

public sealed class SubscriptionAutoStartService(
    ILogger<SubscriptionAutoStartService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval);
        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await AutoStartDueSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while processing subscription schedules.");
            }
        }
    }

    private Task AutoStartDueSchedulesAsync(CancellationToken cancellationToken)
    {
        // Disabled: planned sessions come from SubscriptionPlannedSessionSync;
        // activation only via ActivateSession / "İndi başla".
        return Task.CompletedTask;
    }
}
