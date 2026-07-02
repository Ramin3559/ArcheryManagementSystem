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
            ActiveFromDateLocal = request.ActiveFromDateLocal.Date,
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

        static bool DateRangesOverlap(DateTime aFrom, DateTime aTo, DateTime bFrom, DateTime bTo)
            => aFrom.Date <= bTo.Date && bFrom.Date <= aTo.Date;

        static bool TimeRangesOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
            => aStart < bEnd && bStart < aEnd;

        // Global reservation protection for subscription schedules on a specific lane.
        // If the operator picks a concrete lane, we must ensure it isn't already reserved by another enabled schedule.
        if (!request.IsFullPackage && request.LaneNumber > 0 && request.DurationMinutes > 0)
        {
            var requestedStart = request.StartTimeLocal;
            var requestedEnd = request.StartTimeLocal
                .Add(TimeSpan.FromMinutes(request.DurationMinutes))
                .Add(LaneReservationRules.SessionBuffer);

            var conflicting = existingSchedules
                .Where(s =>
                    s.IsEnabled
                    && s.LaneNumber == request.LaneNumber
                    && s.DayOfWeek == request.DayOfWeek
                    && DateRangesOverlap(s.ActiveFromDateLocal, s.ActiveToDateLocal, request.ActiveFromDateLocal, request.ActiveToDateLocal))
                .FirstOrDefault(s =>
                {
                    var sEnd = s.StartTimeLocal
                        .Add(TimeSpan.FromMinutes(s.DurationMinutes))
                        .Add(LaneReservationRules.SessionBuffer);
                    return TimeRangesOverlap(s.StartTimeLocal, sEnd, requestedStart, requestedEnd);
                });

            if (conflicting is not null)
            {
                var conflictingName = athletes.FirstOrDefault(a => a.Id == conflicting.AthleteId)?.FullName ?? "başqa müştəri";
                throw new InvalidOperationException(
                    $"Təəssüf ki, seçdiyiniz saatda Zolaq {request.LaneNumber} doludur (Müştəri {conflictingName} tərəfindən). Zəhmət olmasa başqa vaxt seçin");
            }
        }

        // Collision check (selected lane type must have at least one free lane).
        if (!request.IsFullPackage)
        {
            var lanes = await repository.GetLanesAsync(cancellationToken);
            var sessions = await repository.GetSessionsAsync(cancellationToken);
            var candidates = request.LaneNumber > 0
                ? lanes.Where(l => l.Number == request.LaneNumber)
                : LaneReservationRules.FilterLanesByPreferredType(lanes, request.PreferredLaneType);

            // If a specific lane is chosen, ensure it isn't busy on ANY occurrence within the selected date range.
            // (Availability UI is date-based; this protects against conflicts beyond just the first occurrence.)
            if (request.LaneNumber > 0)
            {
                var lane = candidates.FirstOrDefault();
                if (lane is not null)
                {
                    var requestedStartLocal = request.StartTimeLocal;
                    var requestedEndLocal = request.StartTimeLocal
                        .Add(TimeSpan.FromMinutes(request.DurationMinutes))
                        ;

                    var laneSessions = sessions.Where(s => s.LaneId == lane.Id);
                    foreach (var s in laneSessions)
                    {
                        var sStartUtc = DateTimeAssumedUtc.AsUtc(s.StartTimeUtc);
                        var sEndUtc = DateTimeAssumedUtc.AsUtc(s.EndTimeUtc);
                        var sStartLocal = sStartUtc.ToLocalTime();
                        var sEndLocal = sEndUtc.ToLocalTime();

                        var sDate = sStartLocal.Date;
                        if (sDate < request.ActiveFromDateLocal.Date || sDate > request.ActiveToDateLocal.Date) continue;
                        if ((int)sDate.DayOfWeek != request.DayOfWeek) continue;

                        var sStartTod = sStartLocal.TimeOfDay;
                        var sEndTod = sEndLocal.TimeOfDay;
                        if (sEndTod <= sStartTod) continue; // defensive (shouldn't happen)

                        if (sStartTod < requestedEndLocal && requestedStartLocal < sEndTod)
                        {
                            var otherName = athletes.FirstOrDefault(a => a.Id == s.AthleteId)?.FullName ?? "başqa müştəri";
                            throw new InvalidOperationException(
                                $"Təəssüf ki, seçdiyiniz saatda Zolaq {request.LaneNumber} doludur (Müştəri {otherName} tərəfindən saat {sEndLocal:HH:mm}-a qədər). Zəhmət olmasa başqa vaxt seçin");
                        }
                    }
                }
            }

            var nextLocal = request.ActiveFromDateLocal.Date;
            for (var guard = 0; guard < 14 && (int)nextLocal.DayOfWeek != request.DayOfWeek; guard++)
            {
                nextLocal = nextLocal.AddDays(1);
            }
            var slotLocal = nextLocal.Add(request.StartTimeLocal);
            var startUtc = DateTime.SpecifyKind(slotLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = startUtc.AddMinutes(request.DurationMinutes);

            var nowUtc = DateTime.UtcNow;
            var hasFree = candidates.Any(lane =>
                sessions.Where(s => s.LaneId == lane.Id).All(s => !LaneReservationRules.OverlapsSession(s, startUtc, endUtc, nowUtc)));

            if (!hasFree)
            {
                if (request.LaneNumber > 0)
                {
                    throw new InvalidOperationException($"Təəssüf ki, seçdiyiniz saatda Zolaq {request.LaneNumber} doludur. Zəhmət olmasa başqa vaxt seçin");
                }

                var label = request.PreferredLaneType == PreferredLaneType.Long ? "Uzun" : "Qısa";
                throw new InvalidOperationException($"Təəssüf ki, seçdiyiniz saatda bütün {label} xətlər doludur. Zəhmət olmasa başqa vaxt seçin");
            }
        }

        var created = await repository.AddSubscriptionScheduleAsync(schedule, cancellationToken);

        // Auto-populate concrete future sessions only when a specific lane is chosen.
        // (LaneNumber == 0 means "auto" and lane is decided later.)
        if (!request.IsFullPackage && request.LaneNumber > 0 && request.DurationMinutes > 0)
        {
            var lane = await repository.GetLaneByNumberAsync(request.LaneNumber, cancellationToken);
            if (lane is not null)
            {
                var startDate = request.ActiveFromDateLocal.Date;
                var endDate = request.ActiveToDateLocal.Date;
                for (var day = startDate; day <= endDate; day = day.AddDays(1))
                {
                    if ((int)day.DayOfWeek != request.DayOfWeek) continue;
                    var slotLocal = day.Add(request.StartTimeLocal);
                    var startUtc = DateTime.SpecifyKind(slotLocal, DateTimeKind.Local).ToUniversalTime();
                    var endUtc = startUtc.AddMinutes(request.DurationMinutes);

                    // Avoid duplicates if already created.
                    var sessions = await repository.GetSessionsAsync(cancellationToken);
                    var exists = sessions.Any(s =>
                        s.SubscriptionScheduleId == created.Id
                        && s.LaneId == lane.Id
                        && DateTimeAssumedUtc.AsUtc(s.StartTimeUtc) == startUtc);
                    if (exists) continue;

                    await repository.AddSessionAsync(new TrainingSession
                    {
                        AthleteId = athlete.Id,
                        LaneId = lane.Id,
                        SubscriptionScheduleId = created.Id,
                        StartTimeUtc = startUtc,
                        EndTimeUtc = endUtc,
                        Status = SessionStatus.Scheduled
                    }, cancellationToken);
                }
            }
        }

        return created.Id;
    }
}
