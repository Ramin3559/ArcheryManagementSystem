using EShooting.Domain.Entities;

namespace EShooting.Application.Common;

/// <summary>Limitsiz çevik (walk-in) abunə — gələndə boş zolaq.</summary>
public static class WalkInSubscriptionRules
{
    public static SubscriptionSchedule? GetActiveWalkInSchedule(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        Guid athleteId,
        DateTime todayLocal)
    {
        var today = todayLocal.Date;
        return schedules
            .Where(s =>
                s.IsEnabled
                && s.IsFullPackage
                && s.AthleteId == athleteId
                && s.ActiveFromDateLocal.Date <= today
                && s.ActiveToDateLocal.Date >= today)
            .OrderByDescending(s => s.ActiveToDateLocal)
            .ThenByDescending(s => s.CreatedAtUtc)
            .FirstOrDefault();
    }

    public static bool HasActiveWalkIn(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        Guid athleteId,
        DateTime todayLocal)
        => GetActiveWalkInSchedule(schedules, athleteId, todayLocal) is not null;
}
