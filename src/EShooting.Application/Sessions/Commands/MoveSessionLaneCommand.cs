using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record MoveSessionLaneResult(
    bool Ok,
    string? Code,
    string? Message,
    int FromLaneNumber,
    int ToLaneNumber,
    bool Swapped,
    Guid? OccupantSessionId,
    string? OccupantName);

public sealed record MoveSessionLaneCommand(
    Guid SessionId,
    int LaneNumber,
    bool AllowSwap = false,
    bool AllowAmateurOnProLane = false) : IRequest<MoveSessionLaneResult>;

public sealed class MoveSessionLaneCommandHandler(
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : IRequestHandler<MoveSessionLaneCommand, MoveSessionLaneResult>
{
    public async Task<MoveSessionLaneResult> Handle(MoveSessionLaneCommand request, CancellationToken cancellationToken)
    {
        if (request.LaneNumber is < 1 or > 11)
        {
            throw new InvalidOperationException("Zolaq nömrəsi 1–11 arasında olmalıdır.");
        }

        var nowUtc = DateTime.UtcNow;
        var session = await repository.GetSessionByIdAsync(request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException("Sessiya tapılmadı.");

        if (session.Status == SessionStatus.Completed)
        {
            throw new InvalidOperationException("Tamamlanmış sessiyanı köçürmək olmaz.");
        }

        if (!SessionActivationRules.HasActivation(session))
        {
            throw new InvalidOperationException("Yalnız aktiv sessiyanı köçürmək olar.");
        }

        if (!IsLiveActivated(session, nowUtc))
        {
            throw new InvalidOperationException("Sessiya hazırda aktiv pəncərədə deyil.");
        }

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var currentLane = lanes.FirstOrDefault(x => x.Id == session.LaneId)
            ?? throw new InvalidOperationException("Cari zolaq tapılmadı.");
        var targetLane = lanes.FirstOrDefault(x => x.Number == request.LaneNumber)
            ?? throw new InvalidOperationException($"{request.LaneNumber} nömrəli zolaq tapılmadı.");

        if (GymLaneRules.IsGymLane(currentLane.Number) || GymLaneRules.IsGymLane(targetLane.Number))
        {
            throw new InvalidOperationException("Trenajor zolağı bu əməliyyatda dəstəklənmir.");
        }

        if (currentLane.Id == targetLane.Id)
        {
            return new MoveSessionLaneResult(
                Ok: true,
                Code: null,
                Message: null,
                FromLaneNumber: currentLane.Number,
                ToLaneNumber: targetLane.Number,
                Swapped: false,
                OccupantSessionId: null,
                OccupantName: null);
        }

        var athlete = await repository.GetAthleteByIdAsync(session.AthleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        static bool IsShortLane(int number) => number is >= 1 and <= 8;
        if (athlete.Category == CustomerCategory.Amateur
            && !IsShortLane(request.LaneNumber)
            && !request.AllowAmateurOnProLane)
        {
            return new MoveSessionLaneResult(
                Ok: false,
                Code: "amateur_pro_lane",
                Message: "Professional zolağa keçirmək istəyirsiniz?",
                FromLaneNumber: currentLane.Number,
                ToLaneNumber: targetLane.Number,
                Swapped: false,
                OccupantSessionId: null,
                OccupantName: null);
        }

        var allSessions = await repository.GetSessionsLightAsync(cancellationToken);
        var occupant = allSessions
            .Where(x => x.Id != session.Id
                && x.LaneId == targetLane.Id
                && x.Status != SessionStatus.Completed
                && IsLiveActivated(x, nowUtc))
            .OrderByDescending(x => DateTimeAssumedUtc.AsUtc(x.StartTimeUtc))
            .FirstOrDefault();

        if (occupant is not null && !request.AllowSwap)
        {
            var occupantAthlete = await repository.GetAthleteByIdAsync(occupant.AthleteId, cancellationToken);
            var occupantName = string.IsNullOrWhiteSpace(occupantAthlete?.FullName)
                ? "Digər müştəri"
                : occupantAthlete!.FullName!;
            return new MoveSessionLaneResult(
                Ok: false,
                Code: "lane_occupied",
                Message: $"{request.LaneNumber} nömrəli zolaqda {occupantName} var. Yer dəyişdirilsin?",
                FromLaneNumber: currentLane.Number,
                ToLaneNumber: targetLane.Number,
                Swapped: false,
                OccupantSessionId: occupant.Id,
                OccupantName: occupantName);
        }

        if (occupant is not null)
        {
            // Swap: vaxtlar toxunulmur — yalnız LaneId dəyişir.
            occupant.LaneId = currentLane.Id;
            session.LaneId = targetLane.Id;
            await repository.UpdateSessionAsync(occupant, cancellationToken);
            await repository.UpdateSessionAsync(session, cancellationToken);
            await notifier.PublishLaneUpdateAsync(currentLane.Number, cancellationToken);
            await notifier.PublishLaneUpdateAsync(targetLane.Number, cancellationToken);
            return new MoveSessionLaneResult(
                Ok: true,
                Code: null,
                Message: null,
                FromLaneNumber: currentLane.Number,
                ToLaneNumber: targetLane.Number,
                Swapped: true,
                OccupantSessionId: occupant.Id,
                OccupantName: null);
        }

        session.LaneId = targetLane.Id;
        await repository.UpdateSessionAsync(session, cancellationToken);
        await notifier.PublishLaneUpdateAsync(currentLane.Number, cancellationToken);
        await notifier.PublishLaneUpdateAsync(targetLane.Number, cancellationToken);
        return new MoveSessionLaneResult(
            Ok: true,
            Code: null,
            Message: null,
            FromLaneNumber: currentLane.Number,
            ToLaneNumber: targetLane.Number,
            Swapped: false,
            OccupantSessionId: null,
            OccupantName: null);
    }

    private static bool IsLiveActivated(TrainingSession session, DateTime nowUtc)
    {
        if (!SessionActivationRules.HasActivation(session) || session.Status == SessionStatus.Completed)
        {
            return false;
        }

        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        var start = session.ActivatedAtUtc is DateTime a
            ? DateTimeAssumedUtc.AsUtc(a)
            : plannedStart;
        var duration = plannedEnd > plannedStart ? plannedEnd - plannedStart : TimeSpan.Zero;
        var end = duration > TimeSpan.Zero ? start + duration : start;

        if (!(plannedEnd > plannedStart))
        {
            if (nowUtc < start)
            {
                return false;
            }

            if (AzerbaijanTime.UtcToLocalDate(start) < AzerbaijanTime.UtcToLocalDate(nowUtc))
            {
                return false;
            }

            return true;
        }

        return nowUtc >= start && nowUtc < end;
    }
}
