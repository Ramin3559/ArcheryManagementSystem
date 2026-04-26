using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Subscriptions.Commands;

public sealed record UpdateSubscriptionScheduleCommand(
    Guid ScheduleId,
    int DayOfWeek,
    TimeSpan StartTimeLocal,
    int DurationMinutes,
    DateTime ActiveFromDateLocal,
    DateTime ActiveToDateLocal,
    PreferredLaneType PreferredLaneType,
    int LaneNumber,
    bool IsFullPackage) : IRequest<Guid>;

public sealed class UpdateSubscriptionScheduleCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpdateSubscriptionScheduleCommand, Guid>
{
    public async Task<Guid> Handle(UpdateSubscriptionScheduleCommand request, CancellationToken cancellationToken)
    {
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

        if (request.LaneNumber is < 0 or > 11)
        {
            throw new InvalidOperationException("LaneNumber must be between 0 and 11.");
        }

        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var existing = schedules.FirstOrDefault(x => x.Id == request.ScheduleId)
            ?? throw new InvalidOperationException("Subscription schedule not found.");

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = athletes.FirstOrDefault(x => x.Id == existing.AthleteId)
            ?? throw new InvalidOperationException("Athlete must be registered first.");

        if (athlete.Category == CustomerCategory.Amateur)
        {
            if (request.LaneNumber >= 9)
            {
                throw new InvalidOperationException("Həvəskar yalnız 1-8 zolaqlarda ola bilər.");
            }
            if (request.PreferredLaneType == PreferredLaneType.Long)
            {
                throw new InvalidOperationException("Amateur can only select Short lane type.");
            }
        }

        // Prevent duplicates for the same athlete/day/time/lane while enabled (excluding self).
        var dup = schedules.FirstOrDefault(s =>
            s.IsEnabled
            && s.Id != existing.Id
            && s.AthleteId == athlete.Id
            && s.DayOfWeek == request.DayOfWeek
            && s.StartTimeLocal == request.StartTimeLocal
            && s.LaneNumber == request.LaneNumber);
        if (dup is not null)
        {
            throw new InvalidOperationException($"DUPLICATE_SUBSCRIPTION_SCHEDULE:{dup.Id}");
        }

        static bool DateRangesOverlap(DateTime aFrom, DateTime aTo, DateTime bFrom, DateTime bTo)
            => aFrom.Date <= bTo.Date && bFrom.Date <= aTo.Date;

        static bool TimeRangesOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
            => aStart < bEnd && bStart < aEnd;

        // Global reservation protection for subscription schedules on a specific lane (exclude self).
        if (!request.IsFullPackage && request.LaneNumber > 0 && request.DurationMinutes > 0)
        {
            var requestedStart = request.StartTimeLocal;
            var requestedEnd = request.StartTimeLocal
                .Add(TimeSpan.FromMinutes(request.DurationMinutes))
                ;

            var conflicting = schedules
                .Where(s =>
                    s.IsEnabled
                    && s.Id != existing.Id
                    && s.LaneNumber == request.LaneNumber
                    && s.DayOfWeek == request.DayOfWeek
                    && DateRangesOverlap(s.ActiveFromDateLocal, s.ActiveToDateLocal, request.ActiveFromDateLocal, request.ActiveToDateLocal))
                .FirstOrDefault(s =>
                {
                    var sEnd = s.StartTimeLocal
                        .Add(TimeSpan.FromMinutes(s.DurationMinutes))
                        ;
                    return TimeRangesOverlap(s.StartTimeLocal, sEnd, requestedStart, requestedEnd);
                });

            if (conflicting is not null)
            {
                var conflictingName = athletes.FirstOrDefault(a => a.Id == conflicting.AthleteId)?.FullName ?? "başqa müştəri";
                throw new InvalidOperationException(
                    $"Təəssüf ki, seçdiyiniz saatda Zolaq {request.LaneNumber} doludur (Müştəri {conflictingName} tərəfindən). Zəhmət olmasa başqa vaxt seçin");
            }
        }

        existing.DayOfWeek = request.DayOfWeek;
        existing.StartTimeLocal = request.StartTimeLocal;
        existing.DurationMinutes = request.DurationMinutes;
        existing.ActiveFromDateLocal = request.ActiveFromDateLocal.Date;
        existing.ActiveToDateLocal = request.ActiveToDateLocal.Date;
        existing.IsEnabled = true;
        existing.PreferredLaneType = request.PreferredLaneType;
        existing.LaneNumber = request.LaneNumber;
        existing.IsFullPackage = request.IsFullPackage;

        // Validate chosen lane (if specified) has capacity for next occurrence.
        if (!request.IsFullPackage)
        {
            var lanes = await repository.GetLanesAsync(cancellationToken);
            var sessions = await repository.GetSessionsAsync(cancellationToken);
            var candidates = request.LaneNumber > 0
                ? lanes.Where(l => l.Number == request.LaneNumber)
                : LaneReservationRules.FilterLanesByPreferredType(lanes, request.PreferredLaneType);

            // If a specific lane is chosen, ensure it isn't busy on ANY occurrence within the selected date range.
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
                        if (sEndTod <= sStartTod) continue;

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

        await repository.UpdateSubscriptionScheduleAsync(existing, cancellationToken);
        return existing.Id;
    }
}

