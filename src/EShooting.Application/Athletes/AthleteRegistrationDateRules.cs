using EShooting.Application.Common;
using EShooting.Domain.Entities;

namespace EShooting.Application.Athletes;

public static class AthleteRegistrationDateRules
{
    /// <summary>
    /// Müştərinin sistemə qeydiyyata alınma tarixi — yalnız CreatedAtUtc.
    /// Abunə / zolaq / paket tarixləri buna qarışmır.
    /// </summary>
    public static DateTime ResolveRegisteredAtUtc(
        Athlete athlete,
        IReadOnlyList<TrainingSession>? sessions = null,
        IReadOnlyList<SubscriptionSchedule>? schedules = null,
        IReadOnlyList<CustomerPackageRecord>? packageRecords = null)
    {
        _ = sessions;
        _ = schedules;
        _ = packageRecords;
        return DateTimeAssumedUtc.AsUtc(athlete.CreatedAtUtc);
    }
}
