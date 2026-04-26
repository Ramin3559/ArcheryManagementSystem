using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record ScheduleSessionCommand(
    Guid AthleteId,
    int LaneNumber,
    DateTime StartTimeUtc,
    int DurationMinutes,
    bool IsEquipmentIssued) : IRequest<Guid>;

public sealed class ScheduleSessionCommandHandler(
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : IRequestHandler<ScheduleSessionCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleSessionCommand request, CancellationToken cancellationToken)
    {
        if (request.DurationMinutes < 0)
        {
            throw new InvalidOperationException("Müddət mənfi ola bilməz.");
        }

        var startTimeUtc = LaneReservationRules.NormalizeToUtc(request.StartTimeUtc);
        var isOpenEnded = request.DurationMinutes == 0;
        var nowUtc = DateTime.UtcNow;

        var lane = await repository.GetLaneByNumberAsync(request.LaneNumber, cancellationToken)
            ?? throw new InvalidOperationException($"{request.LaneNumber} nömrəli zolaq mövcud deyil.");
        var lanes = await repository.GetLanesAsync(cancellationToken);

        var requestedEndTimeUtc = isOpenEnded
            ? startTimeUtc
            : startTimeUtc.AddMinutes(request.DurationMinutes);
        var allSessions = await repository.GetSessionsAsync(cancellationToken);
        var existingLaneSessions = allSessions
            .Where(x => x.LaneId == lane.Id && x.Status != SessionStatus.Completed);

        var hasOverlap = existingLaneSessions.Any(x =>
        {
            var existingStart = DateTimeAssumedUtc.AsUtc(x.StartTimeUtc);
            var existingEnd = DateTimeAssumedUtc.AsUtc(x.EndTimeUtc);
            var existingOpenEnded = !LaneReservationRules.HasValidWindow(existingStart, existingEnd);

            if (existingOpenEnded || isOpenEnded)
            {
                return true;
            }

            return startTimeUtc < existingEnd && requestedEndTimeUtc > existingStart;
        });

        if (hasOverlap)
        {
            var conflict = existingLaneSessions
                .Select(s => new { Session = s, Start = DateTimeAssumedUtc.AsUtc(s.StartTimeUtc), End = DateTimeAssumedUtc.AsUtc(s.EndTimeUtc) })
                .FirstOrDefault(x => LaneReservationRules.OverlapsSession(x.Session, startTimeUtc, requestedEndTimeUtc, nowUtc));

            var athletes = await repository.GetAthletesAsync(cancellationToken);
            var who = conflict is null
                ? "başqa müştəri"
                : (athletes.FirstOrDefault(a => a.Id == conflict.Session.AthleteId)?.FullName ?? "başqa müştəri");
            var untilLocal = conflict is null ? "" : conflict.End.ToLocalTime().ToString("HH:mm");
            var tail = string.IsNullOrWhiteSpace(untilLocal) ? "" : $" ({who} tərəfindən saat {untilLocal}-a qədər)";
            throw new InvalidOperationException($"Bu zolaq seçdiyiniz zaman aralığında tutulub{tail}.");
        }

        if (!isOpenEnded)
        {
            var subscriptionSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);

            if (!LaneReservationRules.HasManualCapacityForSlot(
                    lanes,
                    allSessions,
                    subscriptionSchedules,
                    startTimeUtc,
                    requestedEndTimeUtc,
                    nowUtc))
            {
                throw new InvalidOperationException(
                    "Bu vaxt üçün zolaq təyin edilə bilməz. Abunəçilər üçün rezerv olunmuş boş yerlər saxlanılmalıdır.");
            }

            if (LaneReservationRules.HasSubscriberConflictOnLane(
                    subscriptionSchedules,
                    request.LaneNumber,
                    startTimeUtc,
                    requestedEndTimeUtc))
            {
                throw new InvalidOperationException(
                    $"{request.LaneNumber} nömrəli zolaq həmin vaxt aralığında abunə rezervasiyası ilə üst-üstə düşür.");
            }
        }

        var session = new TrainingSession
        {
            AthleteId = request.AthleteId,
            LaneId = lane.Id,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = requestedEndTimeUtc,
            Status = startTimeUtc <= nowUtc ? SessionStatus.Active : SessionStatus.Scheduled,
            IsEquipmentIssued = request.IsEquipmentIssued,
            EquipmentReturnedAtUtc = null
        };

        var created = await repository.AddSessionAsync(session, cancellationToken);
        await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        return created.Id;
    }
}
