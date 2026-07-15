using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Application.Equipment;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record RegisterGroupOnLaneCommand(
    IReadOnlyCollection<string> AthleteNames,
    int LaneNumber,
    DateTime StartTimeUtc,
    int DurationMinutes,
    bool IsEquipmentIssued,
    bool ActivateImmediately = false,
    IReadOnlyList<SessionEquipmentIssueRequest>? EquipmentIssues = null,
    Guid? IssuedByStaffId = null) : IRequest<RegisterGroupOnLaneResult>;

public sealed record RegisterGroupOnLaneResult(IReadOnlyCollection<RegisterGroupOnLaneItem> Sessions);

public sealed record RegisterGroupOnLaneItem(
    Guid SessionId,
    Guid AthleteId,
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
                IsSubscriber = false,
                IsGroupPlaceholder = true
            }, cancellationToken);
        }

        var equipmentIssues = request.EquipmentIssues ?? [];
        var hasRentalEquipment = equipmentIssues.Any(x => x.IssueType == EquipmentIssueType.Rental);
        var legacyEquipmentFlag = request.IsEquipmentIssued && equipmentIssues.Count == 0;

        var created = await repository.AddSessionAsync(new TrainingSession
        {
            AthleteId = athlete.Id,
            LaneId = lane.Id,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            Status = SessionStatus.Scheduled,
            IsEquipmentIssued = hasRentalEquipment || legacyEquipmentFlag,
            EquipmentReturnedAtUtc = null
        }, cancellationToken);

        if (request.ActivateImmediately)
        {
            SessionActivationRules.MarkActivated(created, nowUtc);
            await repository.UpdateSessionAsync(created, cancellationToken);
        }

        if (equipmentIssues.Count > 0)
        {
            var issueRows = new List<SessionEquipmentIssue>();
            foreach (var issue in equipmentIssues)
            {
                var catalogItem = await repository.GetEquipmentItemByIdAsync(issue.EquipmentItemId, cancellationToken)
                    ?? throw new InvalidOperationException("Seçilmiş avadanlıq tapılmadı.");
                if (!catalogItem.IsActive || catalogItem.IsDeleted)
                {
                    throw new InvalidOperationException($"«{catalogItem.Name}» deaktivdir və verilə bilməz.");
                }

                EquipmentIssuanceRules.ValidateIssueType(catalogItem, issue.IssueType);
                var quantity = issue.Quantity > 0 ? issue.Quantity : 1;
                EquipmentIssuanceRules.ApplyStockOnIssue(catalogItem, issue.IssueType, quantity);
                await repository.UpdateEquipmentItemAsync(catalogItem, cancellationToken);

                issueRows.Add(new SessionEquipmentIssue
                {
                    SessionId = created.Id,
                    EquipmentItemId = catalogItem.Id,
                    IssueType = issue.IssueType,
                    Quantity = quantity,
                    UnitPrice = EquipmentIssuanceRules.ResolveUnitPrice(catalogItem, issue.IssueType),
                    IssuedByStaffId = request.IssuedByStaffId,
                    ReturnedAtUtc = null
                });
            }

            await repository.AddSessionEquipmentIssuesAsync(issueRows, cancellationToken);
        }

        await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        return new RegisterGroupOnLaneResult(
        [
            new RegisterGroupOnLaneItem(created.Id, athlete.Id, athlete.FullName, startTimeUtc, endTimeUtc)
        ]);
    }

    private static string BuildGroupAthleteName(IReadOnlyCollection<string> names)
    {
        var merged = string.Join(", ", names);
        if (merged.Length <= 200)
        {
            return merged;
        }

        return $"{merged[..197]}...";
    }

}
