using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Sessions.Commands;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Web.BackgroundServices;

public sealed class SubscriptionAutoStartService(
    IServiceProvider serviceProvider,
    ILogger<SubscriptionAutoStartService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EarlyTolerance = TimeSpan.FromSeconds(5);

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

    private async Task AutoStartDueSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITrainingCenterRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var nowLocal = DateTime.Now;
        var todayLocal = nowLocal.Date;

        foreach (var schedule in schedules)
        {
            if (!schedule.IsEnabled)
            {
                continue;
            }
            if (schedule.IsFullPackage)
            {
                continue;
            }

            if (schedule.ActiveFromDateLocal.Date > todayLocal || schedule.ActiveToDateLocal.Date < todayLocal)
            {
                continue;
            }

            if (schedule.DayOfWeek != (int)nowLocal.DayOfWeek)
            {
                continue;
            }

            var scheduledLocal = todayLocal.Add(schedule.StartTimeLocal);
            var sinceScheduled = nowLocal - scheduledLocal;
            if (sinceScheduled < -EarlyTolerance)
            {
                continue;
            }

            var scheduledUtc = DateTime.SpecifyKind(scheduledLocal, DateTimeKind.Local).ToUniversalTime();
            if (WasAlreadyProcessed(schedule.LastAutoStartedAtUtc, scheduledUtc))
            {
                continue;
            }

            try
            {
                var sessions = await repository.GetSessionsAsync(cancellationToken);
                var startUtc = scheduledUtc > DateTime.UtcNow ? scheduledUtc : DateTime.UtcNow;
                var endUtc = startUtc.AddMinutes(schedule.DurationMinutes);
                var candidates = schedule.LaneNumber > 0
                    ? lanes.Where(l => l.Number == schedule.LaneNumber)
                    : LaneReservationRules.FilterLanesByPreferredType(lanes, schedule.PreferredLaneType);

                // buffer ləğv edildi
                var cooldownCutoff = DateTime.UtcNow;
                var selectedLane = candidates
                    .OrderBy(x => x.Number)
                    .FirstOrDefault(lane =>
                    {
                        var laneSessions = sessions.Where(s => s.LaneId == lane.Id).ToList();
                        var recentlyUsed = laneSessions.Any(s =>
                        {
                            var end = DateTimeAssumedUtc.AsUtc(s.EndTimeUtc);
                            return end > cooldownCutoff;
                        });
                        if (recentlyUsed) return false;

                        return laneSessions.All(s => !LaneReservationRules.OverlapsSession(s, startUtc, endUtc, DateTime.UtcNow));
                    });

                if (selectedLane is null)
                {
                    logger.LogWarning(
                        "Auto-start skipped for subscription {ScheduleId}: no lane available at {ScheduledLocal}.",
                        schedule.Id,
                        scheduledLocal);
                    continue;
                }

                await mediator.Send(
                    new ScheduleSessionCommand(
                        schedule.AthleteId,
                        selectedLane.Number,
                        startUtc,
                        schedule.DurationMinutes,
                        false),
                    cancellationToken);

                schedule.LastAssignedLaneNumber = selectedLane.Number;
                schedule.LastAutoStartedAtUtc = DateTime.UtcNow;
                await repository.UpdateSubscriptionScheduleAsync(schedule, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Auto-start skipped for subscription schedule {ScheduleId}.", schedule.Id);
            }
        }
    }

    private static bool WasAlreadyProcessed(DateTime? lastAutoStartedAtUtc, DateTime scheduledUtc)
    {
        if (lastAutoStartedAtUtc is null)
        {
            return false;
        }

        return Math.Abs((lastAutoStartedAtUtc.Value - scheduledUtc).TotalMinutes) <= 2;
    }
}
