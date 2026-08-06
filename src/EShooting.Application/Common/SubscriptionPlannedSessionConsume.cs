using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

/// <summary>
/// «Zolağa yaz» / Stop zamanı eyni gün abunə planını bir seansa bağlamaq və qalıq planları bağlamaq.
/// </summary>
public static class SubscriptionPlannedSessionConsume
{
    public static TrainingSession? FindOpenSameDayPlanned(
        IEnumerable<TrainingSession> sessions,
        Guid athleteId,
        DateTime dayLocal)
    {
        var day = dayLocal.Date;
        return sessions
            .Where(s => s.AthleteId == athleteId
                        && s.Status != SessionStatus.Completed
                        && !SessionActivationRules.HasActivation(s)
                        && s.SubscriptionScheduleId is not null
                        && AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(s.StartTimeUtc)) == day)
            .OrderByDescending(s => DateTimeAssumedUtc.AsUtc(s.StartTimeUtc))
            .FirstOrDefault();
    }

    /// <summary>
    /// Eyni gün üçün aktivləşməmiş abunə planlarını Completed edir (təqvimdən silmir).
    /// Sync MissingOnly completed-i yenidən açmır.
    /// </summary>
    public static async Task CompleteLeftoverSameDayPlannedAsync(
        ITrainingCenterRepository repository,
        IEnumerable<TrainingSession> sessions,
        Guid athleteId,
        DateTime dayLocal,
        Guid? excludeSessionId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var day = dayLocal.Date;
        var leftovers = sessions
            .Where(s => s.AthleteId == athleteId
                        && s.Status != SessionStatus.Completed
                        && !SessionActivationRules.HasActivation(s)
                        && s.SubscriptionScheduleId is not null
                        && AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(s.StartTimeUtc)) == day
                        && (excludeSessionId is null || s.Id != excludeSessionId.Value))
            .ToList();

        foreach (var leftover in leftovers)
        {
            SessionHousekeeping.MarkCompleted(leftover, nowUtc);
            await repository.UpdateSessionAsync(leftover, cancellationToken);
        }
    }
}
