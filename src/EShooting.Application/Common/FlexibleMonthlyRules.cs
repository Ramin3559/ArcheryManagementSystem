using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

/// <summary>Aylıq sərbəst: həftə günü yox, təqvim müddəti + gediş kvotası.</summary>
public static class FlexibleMonthlyRules
{
    public static bool IsFlexibleMonthlyPackage(ServicePackage package) =>
        package.SchedulingMode == PackageSchedulingMode.FlexibleMonthly;

    public static bool IsFlexibleMonthlySchedule(SubscriptionSchedule schedule) =>
        schedule.IsFullPackage && schedule.VisitQuota is > 0;

    public static int MonthlyQuota(ServicePackage package) =>
        package.VisitQuota is >= 1 and <= 31 ? package.VisitQuota.Value : 0;

    /// <summary>16 avqust → 16 sentyabr = 1 ay.</summary>
    public static int CalendarMonths(DateTime fromLocal, DateTime toLocal)
    {
        var from = fromLocal.Date;
        var to = toLocal.Date;
        var months = ((to.Year - from.Year) * 12) + (to.Month - from.Month);
        return Math.Max(1, months);
    }

    public static int TotalVisitQuota(int monthlyQuota, int months) =>
        Math.Max(1, monthlyQuota) * Math.Max(1, months);

    public static int TotalVisitQuota(ServicePackage package, DateTime fromLocal, DateTime toLocal) =>
        TotalVisitQuota(MonthlyQuota(package), CalendarMonths(fromLocal, toLocal));

    public static SubscriptionSchedule? GetEnabledSchedule(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        Guid athleteId)
        => schedules
            .Where(s => s.AthleteId == athleteId && s.IsEnabled && IsFlexibleMonthlySchedule(s))
            .OrderByDescending(s => s.ActiveToDateLocal)
            .ThenByDescending(s => s.CreatedAtUtc)
            .FirstOrDefault();

    public static int CountAttendedVisits(
        IEnumerable<TrainingSession> sessions,
        Guid athleteId,
        SubscriptionSchedule schedule)
    {
        var from = schedule.ActiveFromDateLocal.Date;
        return sessions
            .Where(s => s.AthleteId == athleteId)
            .Where(SessionActivationRules.CountsAsAttendedVisit)
            .Count(s =>
            {
                var day = AzerbaijanTime.UtcToLocalDate(s.StartTimeUtc);
                if (s.SubscriptionScheduleId == schedule.Id)
                {
                    return true;
                }

                return day >= from;
            });
    }

    public static int RemainingVisits(
        IEnumerable<TrainingSession> sessions,
        Guid athleteId,
        SubscriptionSchedule schedule)
    {
        var limit = Math.Max(1, schedule.VisitQuota ?? 0);
        return Math.Max(0, limit - CountAttendedVisits(sessions, athleteId, schedule));
    }

    public static void EnsureCanStartVisit(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        IReadOnlyCollection<TrainingSession> sessions,
        Guid athleteId,
        DateTime dayLocal)
    {
        var schedule = GetEnabledSchedule(schedules, athleteId);
        if (schedule is null)
        {
            return;
        }

        var remaining = RemainingVisits(sessions, athleteId, schedule);
        if (remaining <= 0)
        {
            throw new InvalidOperationException(
                "Bu müştərinin paketi bitib (gediş limiti dolub). Paketi yeniləyin və ya birdəfəlik seçin.");
        }

        if (!WeeklyVisitPeriodRules.IsWithinAccessWindow(
                dayLocal,
                schedule.ActiveFromDateLocal,
                schedule.ActiveToDateLocal,
                remaining))
        {
            throw new InvalidOperationException(
                "Bu müştərinin paket müddəti bitib. Paketi yeniləyin və ya birdəfəlik seçin.");
        }
    }
}
