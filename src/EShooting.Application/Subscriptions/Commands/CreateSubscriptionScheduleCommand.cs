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

        if (request.LaneNumber is < 0 or > 11)
        {
            throw new InvalidOperationException("LaneNumber must be between 0 and 11.");
        }

        if (athlete.Category == CustomerCategory.Amateur)
        {
            if (request.LaneNumber >= 9)
            {
                throw new InvalidOperationException("Həvəskar yalnız 1-8 zolaqlarda ola bilər.");
            }
            if (request.PreferredLaneType == PreferredLaneType.Long)
            {
                throw new InvalidOperationException("Həvəskar üçün yalnız qısa xətlər (1-8) mümkündür.");
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
            IsEnabled = true
            ,
            PreferredLaneType = request.PreferredLaneType,
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

        // Per-occurrence slot conflict (Bakı vaxtı + digər abunə override-ları).
        if (!request.IsFullPackage && request.LaneNumber > 0 && request.DurationMinutes > 0)
        {
            var lanes = await repository.GetLanesAsync(cancellationToken);
            var sessions = await repository.GetSessionsAsync(cancellationToken);
            var nowUtc = DateTime.UtcNow;

            for (var day = firstOccurrence; day <= request.ActiveToDateLocal.Date; day = day.AddDays(1))
            {
                if ((int)day.DayOfWeek != request.DayOfWeek) continue;
                if (SubscriptionOccurrenceRules.IsSlotInThePast(day, request.StartTimeLocal, nowLocal)) continue;

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
        }
        else if (!request.IsFullPackage)
        {
            // LaneNumber == 0 (auto): first occurrence must have at least one free preferred-type lane.
            var lanes = await repository.GetLanesAsync(cancellationToken);
            var sessions = await repository.GetSessionsAsync(cancellationToken);
            var candidates = LaneReservationRules.FilterLanesByPreferredType(lanes, request.PreferredLaneType);
            var nextLocal = firstOccurrence;
            var slotLocal = nextLocal.Add(request.StartTimeLocal);
            var startUtc = AzerbaijanTime.NormalizeScheduleInputToUtc(slotLocal);
            var endUtc = startUtc.AddMinutes(request.DurationMinutes);
            var nowUtc = DateTime.UtcNow;
            var hasFree = candidates.Any(lane =>
                sessions.Where(s => s.LaneId == lane.Id).All(s => !LaneReservationRules.OverlapsSession(s, startUtc, endUtc, nowUtc)));

            if (!hasFree)
            {
                var label = request.PreferredLaneType == PreferredLaneType.Long ? "Uzun" : "Qısa";
                throw new InvalidOperationException($"Təəssüf ki, seçdiyiniz saatda bütün {label} xətlər doludur. Zəhmət olmasa başqa vaxt seçin");
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

                    if (SubscriptionSlotConflict.IsLaneSlotBusy(
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
