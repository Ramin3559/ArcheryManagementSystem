using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record RegisterGroupOnLaneCommand(
    IReadOnlyCollection<string> AthleteNames,
    int LaneNumber,
    DateTime StartTimeUtc,
    int DurationMinutes,
    bool IsEquipmentIssued) : IRequest<RegisterGroupOnLaneResult>;

public sealed record RegisterGroupOnLaneResult(IReadOnlyCollection<RegisterGroupOnLaneItem> Sessions);

public sealed record RegisterGroupOnLaneItem(
    Guid SessionId,
    string AthleteName,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc);

public sealed class RegisterGroupOnLaneCommandHandler(
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : IRequestHandler<RegisterGroupOnLaneCommand, RegisterGroupOnLaneResult>
{
    public async Task<RegisterGroupOnLaneResult> Handle(RegisterGroupOnLaneCommand request, CancellationToken cancellationToken)
    {
        var cleanNames = request.AthleteNames
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (cleanNames.Count == 0)
        {
            throw new InvalidOperationException("At least one athlete name is required.");
        }

        if (request.DurationMinutes <= 0)
        {
            throw new InvalidOperationException("DurationMinutes must be greater than zero.");
        }

        var lane = await repository.GetLaneByNumberAsync(request.LaneNumber, cancellationToken)
            ?? throw new InvalidOperationException($"Lane {request.LaneNumber} does not exist.");
        var lanes = await repository.GetLanesAsync(cancellationToken);

        var startTimeUtc = LaneReservationRules.NormalizeToUtc(request.StartTimeUtc);
        var endTimeUtc = startTimeUtc.AddMinutes(request.DurationMinutes);
        var allSessions = await repository.GetSessionsAsync(cancellationToken);
        var sessions = allSessions
            .Where(x => x.LaneId == lane.Id && x.Status != SessionStatus.Completed)
            .ToList();
        var nowUtc = DateTime.UtcNow;
        var subscriptionSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);

        if (!LaneReservationRules.HasManualCapacityForSlot(
                lanes,
                allSessions,
                subscriptionSchedules,
                startTimeUtc,
                endTimeUtc,
                nowUtc))
        {
            throw new InvalidOperationException(
                "Bu vaxt üçün zolaq təyin edilə bilməz. Abunəçilər üçün rezerv olunmuş boş yerlər saxlanılmalıdır.");
        }

        var hasConflict = sessions.Any(x =>
        {
            return LaneReservationRules.OverlapsSession(x, startTimeUtc, endTimeUtc, nowUtc);
        });

        if (hasConflict)
        {
            var conflict = sessions
                .Select(s => new { Session = s, End = DateTimeAssumedUtc.AsUtc(s.EndTimeUtc) })
                .FirstOrDefault(x => LaneReservationRules.OverlapsSession(x.Session, startTimeUtc, endTimeUtc, nowUtc));

            var allAthletes = await repository.GetAthletesAsync(cancellationToken);
            var who = conflict is null
                ? "başqa müştəri"
                : (allAthletes.FirstOrDefault(a => a.Id == conflict.Session.AthleteId)?.FullName ?? "başqa müştəri");
            var untilLocal = conflict is null ? "" : conflict.End.ToLocalTime().ToString("HH:mm");
            var tail = string.IsNullOrWhiteSpace(untilLocal) ? "" : $" ({who} tərəfindən saat {untilLocal}-a qədər)";
            throw new InvalidOperationException($"Bu zolaq seçdiyiniz zaman aralığında tutulub{tail}.");
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var mergedAthleteName = BuildGroupAthleteName(cleanNames);
        var athlete = athletes.FirstOrDefault(x =>
            string.Equals(x.FullName, mergedAthleteName, StringComparison.OrdinalIgnoreCase));

        if (athlete is null)
        {
            athlete = await repository.AddAthleteAsync(new Athlete
            {
                FullName = mergedAthleteName,
                IsSubscriber = false
            }, cancellationToken);
        }

        var created = await repository.AddSessionAsync(new TrainingSession
        {
            AthleteId = athlete.Id,
            LaneId = lane.Id,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            Status = startTimeUtc <= nowUtc && nowUtc < endTimeUtc ? SessionStatus.Active : SessionStatus.Scheduled,
            IsEquipmentIssued = request.IsEquipmentIssued,
            EquipmentReturnedAtUtc = null
        }, cancellationToken);

        await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        return new RegisterGroupOnLaneResult(
        [
            new RegisterGroupOnLaneItem(created.Id, athlete.FullName, startTimeUtc, endTimeUtc)
        ]);
    }

    private static string BuildGroupAthleteName(IReadOnlyCollection<string> names)
    {
        var merged = $"Qrup: {string.Join(", ", names)}";
        if (merged.Length <= 200)
        {
            return merged;
        }

        return $"{merged[..197]}...";
    }

}
