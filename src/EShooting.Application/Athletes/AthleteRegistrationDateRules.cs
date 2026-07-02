using EShooting.Application.Common;
using EShooting.Domain.Entities;

namespace EShooting.Application.Athletes;

public static class AthleteRegistrationDateRules
{
    /// <summary>
    /// Müştərinin sistemə qeydiyyata alınma tarixi — bazada saxlanılan və ya mövcud ən köhnə fəaliyyət.
    /// </summary>
    public static DateTime ResolveRegisteredAtUtc(
        Athlete athlete,
        IReadOnlyList<TrainingSession> sessions,
        IReadOnlyList<SubscriptionSchedule> schedules,
        IReadOnlyList<CustomerPackageRecord> packageRecords)
    {
        var earliest = athlete.CreatedAtUtc;

        foreach (var session in sessions)
        {
            var startUtc = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
            if (startUtc < earliest)
            {
                earliest = startUtc;
            }
        }

        foreach (var schedule in schedules)
        {
            if (schedule.CreatedAtUtc < earliest)
            {
                earliest = schedule.CreatedAtUtc;
            }

            var activeFromUtc = AzerbaijanTime.NormalizeScheduleInputToUtc(schedule.ActiveFromDateLocal);
            if (activeFromUtc < earliest)
            {
                earliest = activeFromUtc;
            }
        }

        foreach (var record in packageRecords)
        {
            if (record.CreatedAtUtc < earliest)
            {
                earliest = record.CreatedAtUtc;
            }
        }

        return earliest;
    }
}
