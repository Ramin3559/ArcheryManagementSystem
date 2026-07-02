using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

/// <summary>
/// Köhnə/bitmiş sessiyaların təmizlənməsi və TV göstərişi üçün filtrlər.
/// </summary>
public static class SessionHousekeeping
{
    private static bool HasActivation(TrainingSession session)
    {
        // Backward-compatible: older rows may have Status=Active without ActivatedAtUtc.
        return session.ActivatedAtUtc is not null || session.Status == SessionStatus.Active;
    }

    private static DateTime ResolveEffectiveStartUtc(TrainingSession session)
    {
        if (session.ActivatedAtUtc is DateTime activated)
        {
            return DateTimeAssumedUtc.AsUtc(activated);
        }

        return DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
    }

    private static DateTime ResolveEffectiveEndUtc(TrainingSession session)
    {
        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        var plannedDuration = plannedEnd > plannedStart ? plannedEnd - plannedStart : TimeSpan.Zero;

        var effectiveStart = ResolveEffectiveStartUtc(session);
        return plannedDuration > TimeSpan.Zero ? effectiveStart + plannedDuration : effectiveStart;
    }

    public static bool IsOpenEnded(TrainingSession session)
    {
        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var end = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        return !LaneReservationRules.HasValidWindow(start, end);
    }

    public static bool ShouldAutoComplete(TrainingSession session, DateTime nowUtc)
    {
        if (session.Status == SessionStatus.Completed)
        {
            return false;
        }

        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);

        // Aktiv edilməyibsə, həmin gün ərzində avtomatik bağlama (müştəri gecikə bilər).
        // Keçmiş günlərdə qalmış planları isə təmizlə.
        if (!HasActivation(session))
        {
            var startLocalDate = plannedStart.ToLocalTime().Date;
            var todayLocalDate = nowUtc.ToLocalTime().Date;
            return startLocalDate < todayLocalDate;
        }

        var start = ResolveEffectiveStartUtc(session);
        var end = ResolveEffectiveEndUtc(session);

        // Gələcək planlı sessiyaları heç vaxt avtomatik bağlama.
        if (nowUtc < start)
        {
            return false;
        }

        // VIP / müddətsiz — keçmiş günlərdə qalmış açıq sessiyaları bağla.
        if (!LaneReservationRules.HasValidWindow(plannedStart, plannedEnd))
        {
            var startLocalDate = start.ToLocalTime().Date;
            var todayLocalDate = nowUtc.ToLocalTime().Date;
            return startLocalDate < todayLocalDate;
        }

        if (nowUtc >= end)
        {
            return true;
        }

        var startLocalDateFixed = start.ToLocalTime().Date;
        var todayLocalDateFixed = nowUtc.ToLocalTime().Date;
        return startLocalDateFixed < todayLocalDateFixed;
    }

    public static bool IsAthleteSessionCurrentlyActive(TrainingSession session, DateTime nowUtc)
    {
        if (session.Status == SessionStatus.Completed)
        {
            return false;
        }

        if (!HasActivation(session))
        {
            return false;
        }

        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        var start = ResolveEffectiveStartUtc(session);
        var end = ResolveEffectiveEndUtc(session);

        if (nowUtc < start)
        {
            return false;
        }

        if (!LaneReservationRules.HasValidWindow(plannedStart, plannedEnd))
        {
            return true;
        }

        return nowUtc < end;
    }

    public static bool IsDisplayableOverdueSession(TrainingSession session, DateTime nowUtc)
    {
        if (session.Status == SessionStatus.Completed)
        {
            return false;
        }

        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);

        if (!HasActivation(session))
        {
            // Gecikmiş (vaxtı keçmiş) planı həmin gün ərzində göstər.
            if (plannedEnd > plannedStart && nowUtc < plannedEnd)
            {
                return false;
            }
            return plannedStart.ToLocalTime().Date == nowUtc.ToLocalTime().Date;
        }

        var start = ResolveEffectiveStartUtc(session);
        var end = ResolveEffectiveEndUtc(session);

        if (!LaneReservationRules.HasValidWindow(plannedStart, plannedEnd))
        {
            return session.Status != SessionStatus.Completed
                && nowUtc >= start
                && start.ToLocalTime().Date == nowUtc.ToLocalTime().Date;
        }

        if (nowUtc < end)
        {
            return false;
        }

        return start.ToLocalTime().Date == nowUtc.ToLocalTime().Date;
    }

    public static void MarkCompleted(TrainingSession session, DateTime nowUtc)
    {
        session.Status = SessionStatus.Completed;
        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var end = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        if (!LaneReservationRules.HasValidWindow(start, end) || nowUtc < end)
        {
            session.EndTimeUtc = nowUtc;
        }
    }
}
