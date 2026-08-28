using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record ActivateSessionCommand(
    Guid SessionId,
    int LaneNumber = 0,
    FacilityUsage? FacilityUsage = null,
    Guid? HandledByStaffId = null) : IRequest<int>;

public sealed class ActivateSessionCommandHandler(
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : IRequestHandler<ActivateSessionCommand, int>
{
    public async Task<int> Handle(ActivateSessionCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var session = await repository.GetSessionByIdAsync(request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException("Sessiya tapılmadı.");

        if (session.Status == SessionStatus.Completed)
        {
            throw new InvalidOperationException("Sessiya artıq tamamlanıb.");
        }

        if (session.ActivatedAtUtc is not null)
        {
            // Already activated: no-op.
            var currentLane = (await repository.GetLanesAsync(cancellationToken)).FirstOrDefault(x => x.Id == session.LaneId);
            return currentLane?.Number ?? 0;
        }

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var subscriptionSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var allSessions = await repository.GetSessionsLightAsync(cancellationToken);

        var dayLocal = AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(session.StartTimeUtc));
        var linkedSchedule = session.SubscriptionScheduleId is Guid sid
            ? subscriptionSchedules.FirstOrDefault(s => s.Id == sid)
            : null;
        var needsLanePick = linkedSchedule is not null
            && SubscriptionPoolCapacity.ResolveExplicitLaneNumber(linkedSchedule, dayLocal) <= 0
            && request.FacilityUsage != FacilityUsage.Gym
            && !GymLaneRules.IsGymLane(request.LaneNumber);

        if (needsLanePick && request.LaneNumber <= 0)
        {
            throw new InvalidOperationException("Bu plan üçün zolaq seçilməlidir — zolaq hələ təyin olunmayıb.");
        }

        var laneNumber = request.LaneNumber;
        if (request.FacilityUsage == FacilityUsage.Gym)
        {
            laneNumber = GymLaneRules.LaneNumber;
        }
        else if (request.FacilityUsage == FacilityUsage.Archery
                 && GymLaneRules.IsGymLane(laneNumber))
        {
            throw new InvalidOperationException("Oxatma üçün zolaq seçin.");
        }

        var lane = laneNumber > 0
            ? lanes.FirstOrDefault(x => x.Number == laneNumber)
            : lanes.FirstOrDefault(x => x.Id == session.LaneId);

        if (lane is null)
        {
            throw new InvalidOperationException("Seçilmiş zolaq tapılmadı.");
        }

        if (!GymLaneRules.IsGymLane(lane.Number))
        {
            // Planlı sessiya və köhnə abunə zolağı zolağı tutmur — yalnız indi aktiv olan.
            var hasOverlap = allSessions
                .Where(x => x.Id != session.Id && x.LaneId == lane.Id)
                .Any(x => SessionHousekeeping.IsAthleteSessionCurrentlyActive(x, nowUtc));

            if (hasOverlap)
            {
                throw new InvalidOperationException("Bu zolaq seçdiyiniz vaxt aralığında tutulub.");
            }
        }

        session.LaneId = lane.Id;
        if (request.FacilityUsage is FacilityUsage usage)
        {
            session.FacilityUsage = usage;
        }
        else
        {
            session.FacilityUsage ??= FacilityUsageRules.InferFromLane(lane.Number);
        }
        if (request.HandledByStaffId is Guid staffId)
        {
            session.HandledByStaffId = staffId;
        }
        SessionActivationRules.MarkActivated(session, nowUtc);
        await repository.UpdateSessionAsync(session, cancellationToken);

        var dayLocalForCleanup = AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(session.StartTimeUtc));
        await SubscriptionPlannedSessionConsume.CompleteLeftoverSameDayPlannedAsync(
            repository,
            allSessions,
            session.AthleteId,
            dayLocalForCleanup,
            excludeSessionId: session.Id,
            nowUtc,
            cancellationToken);

        await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        return lane.Number;
    }
}

