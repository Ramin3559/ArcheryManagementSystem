using EShooting.Domain.Entities;

namespace EShooting.Application.Common;

/// <summary>VIP / limitsiz full paket abunəsi (müddətsiz sessiya, zolağı resepsiya seçir).</summary>
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

    /// <summary>Yalnız VIP (müddətsiz) aktiv abunə — çevik/90 dəq full paket daxil deyil.</summary>
    public static SubscriptionSchedule? GetActiveVipSchedule(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        Guid athleteId,
        DateTime todayLocal)
    {
        var schedule = GetActiveWalkInSchedule(schedules, athleteId, todayLocal);
        return schedule is { DurationMinutes: 0 } ? schedule : null;
    }

    public static bool HasActiveWalkIn(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        Guid athleteId,
        DateTime todayLocal)
        => GetActiveVipSchedule(schedules, athleteId, todayLocal) is not null;
}
