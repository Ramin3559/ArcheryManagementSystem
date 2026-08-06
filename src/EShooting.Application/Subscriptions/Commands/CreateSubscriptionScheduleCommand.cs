using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Subscriptions.Commands;

public sealed record CreateSubscriptionScheduleCommand(
    Guid? AthleteId,
    string AthleteFullName,
    int DayOfWeek,
    TimeSpan StartTimeLocal,
    int DurationMinutes,
    DateTime ActiveFromDateLocal,
    DateTime ActiveToDateLocal,
    PreferredLaneType PreferredLaneType,
    int LaneNumber,
    bool IsFullPackage) : IRequest<Guid>;

public sealed class CreateSubscriptionScheduleCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<CreateSubscriptionScheduleCommand, Guid>
{
    public async Task<Guid> Handle(CreateSubscriptionScheduleCommand request, CancellationToken cancellationToken)
    {
        var athleteFullName = request.AthleteFullName.Trim();
        if (request.AthleteId is null && string.IsNullOrWhiteSpace(athleteFullName))
        {
            throw new InvalidOperationException("AthleteId or AthleteFullName is required.");
        }

        if (request.DayOfWeek is < 0 or > 6)
        {
            throw new InvalidOperationException("DayOfWeek must be between 0 and 6.");
        }

        if (!request.IsFullPackage && request.DurationMinutes <= 0)
        {
            throw new InvalidOperationException("DurationMinutes must be greater than zero.");
        }

        if (request.ActiveToDateLocal.Date < request.ActiveFromDateLocal.Date)
        {
            throw new InvalidOperationException("ActiveToDateLocal must be after ActiveFromDateLocal.");
        }

        var nowLocal = AzerbaijanTime.NowLocal;
        var firstOccurrence = SubscriptionOccurrenceRules.ResolveFirstOccurrenceDateLocal(
            request.ActiveFromDateLocal.Date,
            request.DayOfWeek,
            request.StartTimeLocal,
            nowLocal);
        if (firstOccurrence > request.ActiveToDateLocal.Date)
        {
            throw new InvalidOperationException(
                "Seçilmiş həftə günü/saat üçün abunə müddətində cari vaxtdan sonra keçərli tarix qalmayıb.");
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = request.AthleteId is not null
            ? athletes.FirstOrDefault(x => x.Id == request.AthleteId.Value)
            : athletes.FirstOrDefault(x => string.Equals(x.FullName, athleteFullName, StringComparison.OrdinalIgnoreCase));

        if (athlete is null)
        {
            throw new InvalidOperationException("Athlete must be registered first.");
        }

        if (!GymLaneRules.IsValidScheduleLaneNumber(request.LaneNumber))
        {
            throw new InvalidOperationException("LaneNumber must be between 0 and 11, or 12 (Trenajor).");
        }

        var isGymLane = GymLaneRules.IsGymLane(request.LaneNumber);
        var preferred = SubscriptionPoolCapacity.NormalizeForAthlete(athlete.Category, request.PreferredLaneType);
        if (!isGymLane && athlete.Category == CustomerCategory.Amateur)
        {
            if (request.LaneNumber >= 9)
            {
                throw new InvalidOperationException("Həvəskar yalnız 1-8 zolaqlarda ola bilər.");
            }
            if (preferred == PreferredLaneType.Long)
            {
                throw new InvalidOperationException("Həvəskar üçün yalnız 1–8 zolaqlar mümkündür.");
            }
        }

        if (!athlete.IsSubscriber)
        {
            athlete.IsSubscriber = true;
            await repository.UpdateAthleteAsync(athlete, cancellationToken);
        }

        var schedule = new SubscriptionSchedule
        {
            AthleteId = athlete.Id,
            LaneNumber = request.LaneNumber,
            DayOfWeek = request.DayOfWeek,
            StartTimeLocal = request.StartTimeLocal,
            DurationMinutes = request.DurationMinutes,
            ActiveFromDateLocal = firstOccurrence,
            ActiveToDateLocal = request.ActiveToDateLocal.Date,
            IsEnabled = true,
            PreferredLaneType = preferred,
            IsFullPackage = request.IsFullPackage
        };

        // Prevent duplicates for the same athlete/day/time/lane while enabled.
        var existingSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var dup = existingSchedules.FirstOrDefault(s =>
            s.IsEnabled
            && s.AthleteId == athlete.Id
            && s.DayOfWeek == schedule.DayOfWeek
            && s.StartTimeLocal == schedule.StartTimeLocal
            && s.LaneNumber == schedule.LaneNumber);

        if (dup is not null)
        {
            throw new InvalidOperationException($"DUPLICATE_SUBSCRIPTION_SCHEDULE:{dup.Id}");
        }

        // Per-occurrence conflict: konkret zolaq və ya pool tutumu (Trenajor tutumu yoxlanılmır).
        if (!request.IsFullPackage && request.DurationMinutes > 0 && !isGymLane)
        {
            var lanes = await repository.GetLanesAsync(cancellationToken);
            var sessions = await repository.GetSessionsAsync(cancellationToken);
            var nowUtc = DateTime.UtcNow;

            for (var day = firstOccurrence; day <= request.ActiveToDateLocal.Date; day = day.AddDays(1))
            {
                if ((int)day.DayOfWeek != request.DayOfWeek) continue;
                if (SubscriptionOccurrenceRules.IsSlotInThePast(day, request.StartTimeLocal, nowLocal)) continue;

                if (request.LaneNumber > 0)
                {
                    if (SubscriptionSlotConflict.IsLaneSlotBusy(
                            sessions,
                            existingSchedules,
                            lanes,
                            request.LaneNumber,
                            day,
                            request.StartTimeLocal,
                            request.DurationMinutes,
                            nowUtc))
                    {
                        throw new InvalidOperationException(
                            $"Təəssüf ki, seçdiyiniz saatda Zolaq {request.LaneNumber} doludur ({day:yyyy-MM-dd}). Zəhmət olmasa başqa vaxt seçin");
                    }
                }
                else
                {
                    var snapshot = SubscriptionPoolCapacity.CountForSlot(
                        existingSchedules,
                        day,
                        request.StartTimeLocal,
                        request.DurationMinutes);
                    if (!snapshot.CanFit(preferred))
                    {
                        throw new InvalidOperationException(
                            SubscriptionPoolCapacity.BusyMessage(preferred, snapshot) + $" ({day:yyyy-MM-dd})");
                    }
                }
            }
        }
        var created = await repository.AddSubscriptionScheduleAsync(schedule, cancellationToken);

        // Auto-populate concrete future sessions only when a specific lane is chosen.
        if (!request.IsFullPackage && request.LaneNumber > 0 && request.DurationMinutes > 0)
        {
            var lane = await repository.GetLaneByNumberAsync(request.LaneNumber, cancellationToken);
            if (lane is not null)
            {
                var sessionLanes = await repository.GetLanesAsync(cancellationToken);
                var sessionSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
                var sessions = (await repository.GetSessionsAsync(cancellationToken)).ToList();
                var nowUtc = DateTime.UtcNow;
                var startDate = firstOccurrence;
                var endDate = request.ActiveToDateLocal.Date;
                for (var day = startDate; day <= endDate; day = day.AddDays(1))
                {
                    if ((int)day.DayOfWeek != request.DayOfWeek) continue;
                    if (SubscriptionOccurrenceRules.IsSlotInThePast(day, request.StartTimeLocal, nowLocal)) continue;

                    if (!isGymLane && SubscriptionSlotConflict.IsLaneSlotBusy(
                            sessions,
                            sessionSchedules,
                            sessionLanes,
                            request.LaneNumber,
                            day,
                            request.StartTimeLocal,
                            request.DurationMinutes,
                            nowUtc,
                            excludeScheduleId: created.Id))
                    {
                        throw new InvalidOperationException(
                            $"Təəssüf ki, seçdiyiniz saatda Zolaq {request.LaneNumber} doludur ({day:yyyy-MM-dd}). Zəhmət olmasa başqa vaxt seçin");
                    }

                    var slotLocal = day.Add(request.StartTimeLocal);
                    var startUtc = AzerbaijanTime.NormalizeScheduleInputToUtc(slotLocal);
                    var endUtc = startUtc.AddMinutes(request.DurationMinutes);

                    // Avoid duplicates if already created.
                    var exists = sessions.Any(s =>
                        s.SubscriptionScheduleId == created.Id
                        && s.LaneId == lane.Id
                        && DateTimeAssumedUtc.AsUtc(s.StartTimeUtc) == startUtc);
                    if (exists) continue;

                    var createdSession = await repository.AddSessionAsync(new TrainingSession
                    {
                        AthleteId = athlete.Id,
                        LaneId = lane.Id,
                        SubscriptionScheduleId = created.Id,
                        StartTimeUtc = startUtc,
                        EndTimeUtc = endUtc,
                        Status = SessionStatus.Scheduled
                    }, cancellationToken);
                    sessions.Add(createdSession);
                }
            }
        }

        return created.Id;
    }
}
