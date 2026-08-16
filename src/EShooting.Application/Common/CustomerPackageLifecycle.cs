using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

/// <summary>
/// Mövcud abunənin «bitib / hələ aktiv» statusu — paket yeniləmə ödənişi üçün.
/// Qalıq gediş varsa (carryover) bitmiş sayılmır.
/// </summary>
public static class CustomerPackageLifecycle
{
    public static bool IsCurrentPackageEnded(
        Guid athleteId,
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        IReadOnlyCollection<TrainingSession> sessions,
        DateTime todayLocal)
    {
        var active = schedules
            .Where(s => s.AthleteId == athleteId && s.IsEnabled)
            .ToList();
        if (active.Count == 0)
        {
            return true;
        }

        var fixedWeekly = active.Where(s => !s.IsFullPackage).ToList();
        var flexibleMonthly = active.Where(FlexibleMonthlyRules.IsFlexibleMonthlySchedule).ToList();
        var fullPackages = active
            .Where(s => s.IsFullPackage && !FlexibleMonthlyRules.IsFlexibleMonthlySchedule(s))
            .ToList();

        if (flexibleMonthly.Count > 0)
        {
            var schedule = flexibleMonthly
                .OrderByDescending(s => s.ActiveToDateLocal)
                .ThenByDescending(s => s.CreatedAtUtc)
                .First();
            var flexRemaining = FlexibleMonthlyRules.RemainingVisits(sessions, athleteId, schedule);
            if (todayLocal.Date > WeeklyVisitPeriodRules.MakeupDeadline(schedule.ActiveToDateLocal))
            {
                return true;
            }

            return flexRemaining <= 0;
        }

        if (fullPackages.Count > 0 && fixedWeekly.Count == 0)
        {
            // Limitsiz / walk-in full — yalnız müddət bitibsə yeniləmə lazımdır.
            var to = fullPackages.Max(s => s.ActiveToDateLocal.Date);
            return todayLocal.Date > to;
        }

        if (fixedWeekly.Count == 0)
        {
            return true;
        }

        var periodFrom = fixedWeekly.Min(s => s.ActiveFromDateLocal.Date);
        var periodTo = fixedWeekly.Max(s => s.ActiveToDateLocal.Date);
        var weeklyDays = fixedWeekly.Select(s => s.DayOfWeek).Distinct().Count();
        var visitLimit = WeeklyVisitPeriodRules.ResolveVisitLimit(
            fixedWeekly.Select(s => (
                s.DayOfWeek,
                (IReadOnlySet<string>)SubscriptionOccurrenceJson.DeserializeExcluded(s.ExcludedOccurrenceDatesJson))),
            periodFrom,
            periodTo,
            weeklyDays);

        var enabledIds = active.Select(s => s.Id).ToHashSet();
        var visited = sessions
            .Where(s => s.AthleteId == athleteId)
            .Where(SessionActivationRules.CountsAsAttendedVisit)
            .Count(s =>
            {
                var day = AzerbaijanTime.UtcToLocalDate(s.StartTimeUtc);
                var onCurrentPlan = s.SubscriptionScheduleId is Guid sid && enabledIds.Contains(sid);
                var inPeriod = day >= periodFrom;
                return onCurrentPlan || (inPeriod && s.SubscriptionScheduleId is not null);
            });

        var remaining = Math.Max(0, visitLimit - visited);
        // Qalıq gediş makeup pəncərəsi bağlanıbsa — paket bitmiş sayılır.
        if (todayLocal.Date > WeeklyVisitPeriodRules.MakeupDeadline(periodTo))
        {
            return true;
        }

        return remaining <= 0;
    }
}
