using EShooting.Domain.Entities;

namespace EShooting.Application.Common;

/// <summary>
/// Abune zolaq/saat toqquşmasi — Baki vaxti ve occurrence override nezere alinir.
/// </summary>
public static class SubscriptionSlotConflict
{
    public static (DateTime StartUtc, DateTime EndUtc) ToUtcWindow(
        DateTime dayLocal,
        TimeSpan startTimeLocal,
        int durationMinutes)
    {
        var slotLocal = DateTime.SpecifyKind(dayLocal.Date.Add(startTimeLocal), DateTimeKind.Unspecified);
        var startUtc = AzerbaijanTime.NormalizeScheduleInputToUtc(slotLocal);
        var endUtc = startUtc.AddMinutes(Math.Max(0, durationMinutes));
        return (startUtc, endUtc);
    }

    public static bool IsLaneSlotBusy(
        IReadOnlyCollection<TrainingSession> sessions,
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        IReadOnlyCollection<Lane> lanes,
        int laneNumber,
        DateTime dayLocal,
        TimeSpan startTimeLocal,
        int durationMinutes,
        DateTime nowUtc,
        Guid? excludeScheduleId = null)
    {
        if (laneNumber <= 0 || durationMinutes <= 0)
        {
            return true;
        }

        var lane = lanes.FirstOrDefault(l => l.Number == laneNumber);
        if (lane is null)
        {
            return true;
        }

        var (startUtc, endUtc) = ToUtcWindow(dayLocal, startTimeLocal, durationMinutes);
        if (endUtc <= startUtc)
        {
            return true;
        }

        if (sessions.Any(s =>
                s.LaneId == lane.Id
                && LaneReservationRules.OverlapsSession(s, startUtc, endUtc, nowUtc)))
        {
            return true;
        }

        var reqStartLocal = dayLocal.Date.Add(startTimeLocal);
        var reqEndLocal = reqStartLocal.AddMinutes(durationMinutes);

        foreach (var schedule in schedules)
        {
            if (!schedule.IsEnabled || schedule.IsFullPackage)
            {
                continue;
            }

            if (excludeScheduleId is Guid exId && schedule.Id == exId)
            {
                continue;
            }

            if (!SubscriptionOccurrenceJson.TryResolveOccurrence(
                    schedule,
                    dayLocal,
                    out var otherStart,
                    out var otherDuration,
                    out var otherLane))
            {
                continue;
            }

            if (otherLane <= 0)
            {
                otherLane = schedule.LastAssignedLaneNumber ?? 0;
            }

            if (otherLane != laneNumber || otherDuration <= 0)
            {
                continue;
            }

            var otherStartLocal = dayLocal.Date.Add(otherStart);
            var otherEndLocal = otherStartLocal.AddMinutes(otherDuration);
            if (reqStartLocal < otherEndLocal && reqEndLocal > otherStartLocal)
            {
                return true;
            }
        }

        return false;
    }
}
