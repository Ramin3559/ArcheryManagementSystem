using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Subscriptions.Commands;

public sealed record ScheduleExceptionOverride(
    DateTime DateLocal,
    int LaneNumber,
    TimeSpan StartTimeLocal);

public sealed record CreateSubscriptionScheduleWithExceptionsCommand(
    Guid? AthleteId,
    string AthleteFullName,
    int DayOfWeek,
    TimeSpan StartTimeLocal,
    int DurationMinutes,
    DateTime ActiveFromDateLocal,
    DateTime ActiveToDateLocal,
    PreferredLaneType PreferredLaneType,
    int LaneNumber,
    bool IsFullPackage,
    IReadOnlyList<ScheduleExceptionOverride> Overrides) : IRequest<Guid>;

public sealed class CreateSubscriptionScheduleWithExceptionsCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<CreateSubscriptionScheduleWithExceptionsCommand, Guid>
{
    public async Task<Guid> Handle(CreateSubscriptionScheduleWithExceptionsCommand request, CancellationToken cancellationToken)
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

        if (request.IsFullPackage)
        {
            throw new InvalidOperationException("Full paket üçün istisna ilə cədvəl yaradılmır.");
        }

        if (request.DurationMinutes <= 0)
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

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = request.AthleteId is not null
            ? athletes.FirstOrDefault(x => x.Id == request.AthleteId.Value)
            : athletes.FirstOrDefault(x => string.Equals(x.FullName, athleteFullName, StringComparison.OrdinalIgnoreCase));

        if (athlete is null)
        {
            throw new InvalidOperationException("Athlete must be registered first.");
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

        // Prevent duplicates for the same athlete/day/time/lane while enabled.
        var existingSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var dup = existingSchedules.FirstOrDefault(s =>
            s.IsEnabled
            && s.AthleteId == athlete.Id
            && s.DayOfWeek == request.DayOfWeek
            && s.StartTimeLocal == request.StartTimeLocal
            && s.LaneNumber == request.LaneNumber);
        if (dup is not null)
        {
            throw new InvalidOperationException($"DUPLICATE_SUBSCRIPTION_SCHEDULE:{dup.Id}");
        }

        var baseLaneNumber = request.LaneNumber;
        if (baseLaneNumber <= 0)
        {
            throw new InvalidOperationException("İstisna redaktəsi üçün konkret zolaq seçilməlidir.");
        }

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;

        var overridesByDate = (request.Overrides ?? Array.Empty<ScheduleExceptionOverride>())
            .GroupBy(x => x.DateLocal.Date)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DateLocal).First());

        // Validate overrides
        foreach (var ov in overridesByDate.Values)
        {
            if (ov.DateLocal.Date < request.ActiveFromDateLocal.Date || ov.DateLocal.Date > request.ActiveToDateLocal.Date)
            {
                throw new InvalidOperationException("İstisna tarixi abunə tarix aralığından kənardadır.");
            }
            if ((int)ov.DateLocal.DayOfWeek != request.DayOfWeek)
            {
                throw new InvalidOperationException("İstisna tarixi seçilmiş həftə gününə uyğun deyil.");
            }
            if (ov.LaneNumber is < 1 or > 11)
            {
                throw new InvalidOperationException("İstisna üçün zolaq nömrəsi yanlışdır.");
            }
            if (athlete.Category == CustomerCategory.Amateur && ov.LaneNumber >= 9)
            {
                throw new InvalidOperationException("Həvəskar istisnada da yalnız 1-8 zolaqlarda ola bilər.");
            }
        }

        var schedule = new SubscriptionSchedule
        {
            AthleteId = athlete.Id,
            LaneNumber = baseLaneNumber,
            DayOfWeek = request.DayOfWeek,
            StartTimeLocal = request.StartTimeLocal,
            DurationMinutes = request.DurationMinutes,
            ActiveFromDateLocal = request.ActiveFromDateLocal.Date,
            ActiveToDateLocal = request.ActiveToDateLocal.Date,
            IsEnabled = true,
            PreferredLaneType = request.PreferredLaneType,
            IsFullPackage = false
        };

        var createdSchedule = await repository.AddSubscriptionScheduleAsync(schedule, cancellationToken);

        // Create sessions for every occurrence, applying overrides.
        var from = request.ActiveFromDateLocal.Date;
        var to = request.ActiveToDateLocal.Date;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if ((int)day.DayOfWeek != request.DayOfWeek) continue;

            var ov = overridesByDate.TryGetValue(day, out var o) ? o : null;
            var laneNumber = ov?.LaneNumber ?? baseLaneNumber;
            var startTimeLocal = ov?.StartTimeLocal ?? request.StartTimeLocal;

            var lane = lanes.FirstOrDefault(l => l.Number == laneNumber)
                ?? await repository.GetLaneByNumberAsync(laneNumber, cancellationToken);
            if (lane is null)
            {
                throw new InvalidOperationException($"Zolaq {laneNumber} tapılmadı.");
            }

            var slotLocal = day.Add(startTimeLocal);
            var startUtc = DateTime.SpecifyKind(slotLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = startUtc.AddMinutes(request.DurationMinutes);

            // Prevent overlap with existing sessions on that lane.
            var laneBusy = sessions
                .Where(s => s.LaneId == lane.Id)
                .Any(s => LaneReservationRules.OverlapsSession(s, startUtc, endUtc, nowUtc));
            if (laneBusy)
            {
                var label = day.ToString("yyyy-MM-dd");
                throw new InvalidOperationException($"KONFLIKT_HƏLL_OLUNMAYIB:{label}");
            }

            // Prevent overlap with existing enabled subscription schedules that reserve the same lane.
            var reqStartLocal = slotLocal;
            var reqEndLocal = slotLocal.AddMinutes(request.DurationMinutes);
            var reservedBySchedule = existingSchedules.Any(s =>
            {
                if (!s.IsEnabled) return false;
                if (s.Id == createdSchedule.Id) return false;
                if (day < s.ActiveFromDateLocal.Date || day > s.ActiveToDateLocal.Date) return false;
                if (s.DayOfWeek != (int)day.DayOfWeek) return false;
                var reservedLane = s.LastAssignedLaneNumber ?? (s.LaneNumber > 0 ? s.LaneNumber : (int?)null);
                if (reservedLane != laneNumber) return false;
                var subStart = day.Add(s.StartTimeLocal);
                var subEnd = subStart.AddMinutes(s.DurationMinutes);
                return reqStartLocal < subEnd && reqEndLocal > subStart;
            });
            if (reservedBySchedule)
            {
                var label = day.ToString("yyyy-MM-dd");
                throw new InvalidOperationException($"KONFLIKT_HƏLL_OLUNMAYIB:{label}");
            }

            await repository.AddSessionAsync(new TrainingSession
            {
                AthleteId = athlete.Id,
                LaneId = lane.Id,
                SubscriptionScheduleId = createdSchedule.Id,
                StartTimeUtc = startUtc,
                EndTimeUtc = endUtc,
                Status = SessionStatus.Scheduled
            }, cancellationToken);
        }

        return createdSchedule.Id;
    }
}

