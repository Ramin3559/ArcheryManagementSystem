using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

public static class LaneReservationRules
{
    public static readonly TimeSpan SessionBuffer = TimeSpan.Zero;

    public static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public static bool HasValidWindow(DateTime startUtc, DateTime endUtc) => endUtc > startUtc;

    public static bool HasValidWindow(TrainingSession session)
    {
        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var end = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        return HasValidWindow(start, end);
    }

    public static bool IsOpenEndedAndStarted(TrainingSession session, DateTime nowUtc)
    {
        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var end = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        return end <= start && nowUtc >= start;
    }

    public static bool OverlapsSession(TrainingSession session, DateTime requestedStartUtc, DateTime requestedEndUtc, DateTime nowUtc)
    {
        if (session.Status == SessionStatus.Completed)
        {
            return false;
        }

        var existingStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var existingEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        if (!HasValidWindow(existingStart, existingEnd))
        {
            return IsOpenEndedAndStarted(session, nowUtc);
        }

        // Global overlap rule (buffer is persisted in EndTimeUtc).
        return requestedStartUtc < existingEnd && requestedEndUtc > existingStart;
    }

    public static bool HasSubscriberConflictOnLane(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        int laneNumber,
        DateTime requestedStartUtc,
        DateTime requestedEndUtc)
    {
        var requestedStartLocal = requestedStartUtc.ToLocalTime();
        var requestedEndLocal = requestedEndUtc.ToLocalTime();
        var requestedDateLocal = requestedStartLocal.Date;

        return schedules.Any(schedule =>
        {
            if (!schedule.IsEnabled)
            {
                return false;
            }

            var reservedLane = schedule.LastAssignedLaneNumber
                ?? (schedule.LaneNumber > 0 ? schedule.LaneNumber : null);
            if (reservedLane != laneNumber)
            {
                return false;
            }

            if (requestedDateLocal < schedule.ActiveFromDateLocal.Date
                || requestedDateLocal > schedule.ActiveToDateLocal.Date)
            {
                return false;
            }

            if (schedule.DayOfWeek != (int)requestedDateLocal.DayOfWeek)
            {
                return false;
            }

            var subscriberStartLocal = requestedDateLocal.Add(schedule.StartTimeLocal);
            var subscriberEndLocal = subscriberStartLocal.AddMinutes(schedule.DurationMinutes);
            return requestedStartLocal < subscriberEndLocal && requestedEndLocal > subscriberStartLocal;
        });
    }

    public static int GetSubscriberDemandForSlot(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        DateTime requestedStartUtc,
        DateTime requestedEndUtc)
    {
        var requestedStartLocal = requestedStartUtc.ToLocalTime();
        var requestedEndLocal = requestedEndUtc.ToLocalTime();
        var requestedDateLocal = requestedStartLocal.Date;

        return schedules.Count(schedule => IsSubscriberDemandingSlot(schedule, requestedDateLocal, requestedStartLocal, requestedEndLocal));
    }

    public static bool HasManualCapacityForSlot(
        IReadOnlyCollection<Lane> lanes,
        IReadOnlyCollection<TrainingSession> sessions,
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        DateTime requestedStartUtc,
        DateTime requestedEndUtc,
        DateTime nowUtc,
        int additionalManualLaneCount = 1)
    {
        var totalLanes = lanes.Count;
        if (totalLanes <= 0)
        {
            return false;
        }

        var subscriberDemand = GetSubscriberDemandForSlot(schedules, requestedStartUtc, requestedEndUtc);
        var maxManualOccupancy = totalLanes - subscriberDemand;
        if (maxManualOccupancy < 0)
        {
            maxManualOccupancy = 0;
        }

        var requestedStartLocal = requestedStartUtc.ToLocalTime();
        var requestedEndLocal = requestedEndUtc.ToLocalTime();
        var requestedDateLocal = requestedStartLocal.Date;
        var subscriberAthleteIds = schedules
            .Where(schedule => IsSubscriberDemandingSlot(schedule, requestedDateLocal, requestedStartLocal, requestedEndLocal))
            .Select(schedule => schedule.AthleteId)
            .ToHashSet();

        var existingManualOccupancy = sessions
            .Where(session => OverlapsSession(session, requestedStartUtc, requestedEndUtc, nowUtc))
            .Where(session => !subscriberAthleteIds.Contains(session.AthleteId))
            .Select(session => session.LaneId)
            .Distinct()
            .Count();

        return existingManualOccupancy + additionalManualLaneCount <= maxManualOccupancy;
    }

    public static Lane? SelectAvailableLane(
        IReadOnlyCollection<Lane> lanes,
        IReadOnlyCollection<TrainingSession> sessions,
        DateTime requestedStartUtc,
        DateTime requestedEndUtc,
        DateTime nowUtc)
    {
        return lanes
            .OrderBy(x => x.Number)
            .FirstOrDefault(lane =>
                sessions
                    .Where(session => session.LaneId == lane.Id)
                    .All(session => !OverlapsSession(session, requestedStartUtc, requestedEndUtc, nowUtc)));
    }

    public static IReadOnlyCollection<Lane> FilterLanesByPreferredType(
        IReadOnlyCollection<Lane> lanes,
        PreferredLaneType preferred)
    {
        return preferred switch
        {
            PreferredLaneType.Short => lanes.Where(x => x.Number is >= 1 and <= 8).ToList(),
            PreferredLaneType.Long => lanes.Where(x => x.Number is >= 9 and <= 11).ToList(),
            _ => lanes.ToList()
        };
    }

    public static Lane? SelectStrictlyEmptyLane(
        IReadOnlyCollection<Lane> lanes,
        IReadOnlyCollection<TrainingSession> sessions)
    {
        return lanes
            .OrderBy(x => x.Number)
            .FirstOrDefault(lane =>
                sessions
                    .Where(session => session.LaneId == lane.Id)
                    .All(session => session.Status == SessionStatus.Completed));
    }

    private static bool IsSubscriberDemandingSlot(
        SubscriptionSchedule schedule,
        DateTime requestedDateLocal,
        DateTime requestedStartLocal,
        DateTime requestedEndLocal)
    {
        if (!schedule.IsEnabled)
        {
            return false;
        }

        if (requestedDateLocal < schedule.ActiveFromDateLocal.Date
            || requestedDateLocal > schedule.ActiveToDateLocal.Date)
        {
            return false;
        }

        if (schedule.DayOfWeek != (int)requestedDateLocal.DayOfWeek)
        {
            return false;
        }

        var subscriberStartLocal = requestedDateLocal.Add(schedule.StartTimeLocal);
        var subscriberEndLocal = subscriberStartLocal.AddMinutes(schedule.DurationMinutes);
        return requestedStartLocal < subscriberEndLocal && requestedEndLocal > subscriberStartLocal;
    }
}
