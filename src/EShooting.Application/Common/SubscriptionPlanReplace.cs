using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

/// <summary>
/// Paket yenilənəndə köhnə plandan qalan, aktivləşməmiş seansları bağlayır.
/// Gəliş (ActivatedAtUtc) və ödənişə toxunmur.
/// </summary>
public static class SubscriptionPlanReplace
{
    public static async Task CompleteOrphanScheduledAsync(
        ITrainingCenterRepository repository,
        Guid athleteId,
        IReadOnlyCollection<Guid> keepScheduleIds,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var keep = keepScheduleIds.Where(id => id != Guid.Empty).ToHashSet();
        var sessions = await repository.GetSessionsLightAsync(cancellationToken);
        foreach (var session in sessions.Where(s =>
                     s.AthleteId == athleteId
                     && s.Status != SessionStatus.Completed
                     && !SessionActivationRules.HasActivation(s)
                     && s.SubscriptionScheduleId is Guid sid
                     && !keep.Contains(sid)))
        {
            SessionHousekeeping.MarkCompleted(session, nowUtc);
            await repository.UpdateSessionAsync(session, cancellationToken);
        }
    }
}
