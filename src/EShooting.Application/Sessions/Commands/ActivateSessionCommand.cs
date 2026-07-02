using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record ActivateSessionCommand(
    Guid SessionId,
    int LaneNumber = 0) : IRequest<int>;

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

        var duration = SessionTimingRules.ResolvePlannedDuration(session);
        var requestedStartUtc = nowUtc;
        var requestedEndUtc = duration > TimeSpan.Zero ? nowUtc.Add(duration) : nowUtc;

        var lane = request.LaneNumber > 0
            ? lanes.FirstOrDefault(x => x.Number == request.LaneNumber)
            : lanes.FirstOrDefault(x => x.Id == session.LaneId);

        if (lane is null)
        {
            throw new InvalidOperationException("Seçilmiş zolaq tapılmadı.");
        }

        if (!GymLaneRules.IsGymLane(lane.Number) && duration > TimeSpan.Zero)
        {
            if (LaneReservationRules.HasSubscriberConflictOnLane(subscriptionSchedules, lane.Number, requestedStartUtc, requestedEndUtc))
            {
                throw new InvalidOperationException($"{lane.Number} nömrəli zolaq həmin vaxt aralığında abunə rezervasiyası ilə üst-üstə düşür.");
            }

            var hasOverlap = allSessions
                .Where(x => x.Id != session.Id && x.LaneId == lane.Id)
                .Any(x => LaneReservationRules.OverlapsSession(x, requestedStartUtc, requestedEndUtc, nowUtc));

            if (hasOverlap)
            {
                throw new InvalidOperationException("Bu zolaq seçdiyiniz vaxt aralığında tutulub.");
            }
        }

        session.LaneId = lane.Id;
        session.ActivatedAtUtc = nowUtc;
        session.Status = SessionStatus.Active;
        await repository.UpdateSessionAsync(session, cancellationToken);

        await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        return lane.Number;
    }
}

internal static class SessionTimingRules
{
    public static TimeSpan ResolvePlannedDuration(EShooting.Domain.Entities.TrainingSession session)
    {
        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        if (plannedEnd > plannedStart)
        {
            return plannedEnd - plannedStart;
        }
        return TimeSpan.Zero;
    }
}

