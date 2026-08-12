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
        var fullPackages = active.Where(s => s.IsFullPackage).ToList();

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
        var visitLimit = WeeklyVisitPeriodRules.CountPlannedOccurrences(
            fixedWeekly.Select(s => (
                s.DayOfWeek,
                (IReadOnlySet<string>)SubscriptionOccurrenceJson.DeserializeExcluded(s.ExcludedOccurrenceDatesJson))),
            periodFrom,
            periodTo);
        if (visitLimit <= 0)
        {
            visitLimit = Math.Max(1, fixedWeekly.Select(s => s.DayOfWeek).Distinct().Count());
        }

        var visited = sessions
            .Where(s => s.AthleteId == athleteId)
            .Where(s => s.Status is SessionStatus.Active or SessionStatus.Completed)
            .Select(s => AzerbaijanTime.UtcToLocalDate(s.StartTimeUtc))
            .Count(d => d >= periodFrom);

        var remaining = Math.Max(0, visitLimit - visited);
        return remaining <= 0;
    }
}
