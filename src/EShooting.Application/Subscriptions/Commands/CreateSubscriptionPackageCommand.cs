using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Subscriptions.Commands;

public sealed record CreateSubscriptionPackageCommand(
    string AthleteFullName,
    string DayPattern,
    int VisitsCount,
    TimeSpan StartTimeLocal,
    int DurationMinutes,
    DateTime StartDateLocal,
    DateTime? EndDateLocal,
    IReadOnlyDictionary<int, PreferredLaneType> PreferredLaneTypesByDayOfWeek,
    bool IsFullPackage,
    Guid? ServicePackageId = null) : IRequest<CreateSubscriptionPackageResult>;

public sealed record CreateSubscriptionPackageResult(
    int CreatedCount,
    DateTime FirstSessionDateLocal,
    DateTime LastSessionDateLocal);

public sealed class CreateSubscriptionPackageCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<CreateSubscriptionPackageCommand, CreateSubscriptionPackageResult>
{
    private static readonly int[] OddDayPattern = [1, 3, 5];
    private static readonly int[] EvenDayPattern = [2, 4, 6];

    public async Task<CreateSubscriptionPackageResult> Handle(
        CreateSubscriptionPackageCommand request,
        CancellationToken cancellationToken)
    {
        var athleteFullName = request.AthleteFullName.Trim();
        if (string.IsNullOrWhiteSpace(athleteFullName))
        {
            throw new InvalidOperationException("AthleteFullName is required.");
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = athletes.FirstOrDefault(x =>
            string.Equals(x.FullName, athleteFullName, StringComparison.OrdinalIgnoreCase));

        if (athlete is null)
        {
            throw new InvalidOperationException("Athlete must be registered first.");
        }
        else if (!athlete.IsSubscriber)
        {
            athlete.IsSubscriber = true;
        }

        ServicePackage? soldPackage = null;
        if (request.ServicePackageId is Guid pkgId && pkgId != Guid.Empty)
        {
            soldPackage = await repository.GetServicePackageByIdAsync(pkgId, cancellationToken);
        }

        var isFlexibleMonthly = soldPackage is not null && FlexibleMonthlyRules.IsFlexibleMonthlyPackage(soldPackage);
        if (request.IsFullPackage || isFlexibleMonthly)
        {
            var startDate = request.StartDateLocal.Date;
            var endDate = request.EndDateLocal?.Date
                ?? (isFlexibleMonthly ? startDate.AddMonths(1) : startDate.AddDays(30));
            if (endDate < startDate)
            {
                throw new InvalidOperationException("Abunə bitmə tarixi başlanğıcdan əvvəl ola bilməz.");
            }

            var durationMinutes = request.DurationMinutes;
            if (durationMinutes < 0 || durationMinutes > 600)
            {
                throw new InvalidOperationException("DurationMinutes must be between 0 and 600.");
            }

            if (isFlexibleMonthly)
            {
                durationMinutes = Math.Max(1, soldPackage!.SessionDurationMinutes);
            }

            var visitQuota = isFlexibleMonthly
                ? FlexibleMonthlyRules.TotalVisitQuota(soldPackage!, startDate, endDate)
                : (int?)null;

            var currentSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
            foreach (var old in currentSchedules.Where(x =>
                         x.AthleteId == athlete.Id
                         && x.IsEnabled
                         && (x.IsFullPackage || isFlexibleMonthly)))
            {
                old.IsEnabled = false;
                await repository.UpdateSubscriptionScheduleAsync(old, cancellationToken);
            }

            athlete.IsSubscriber = true;
            athlete.IsFullPackage = true;
            // VIP yalnız VIP paket növündə; Limitsiz müddətsiz (DurationMinutes=0) VIP sayılmır.
            athlete.IsVip = soldPackage is not null && ServicePackageRules.IsVipPackage(soldPackage);
            await repository.UpdateAthleteAsync(athlete, cancellationToken);

            await repository.AddSubscriptionScheduleAsync(new SubscriptionSchedule
            {
                AthleteId = athlete.Id,
                LaneNumber = soldPackage is { Scope: PackageScope.Gym } || soldPackage?.BillingType == PackageBillingType.Gym
                    ? GymLaneRules.LaneNumber
                    : 0,
                DayOfWeek = 0,
                StartTimeLocal = TimeSpan.Zero,
                DurationMinutes = durationMinutes,
                ActiveFromDateLocal = startDate,
                ActiveToDateLocal = endDate,
                IsEnabled = true,
                PreferredLaneType = PreferredLaneType.Any,
                IsFullPackage = true,
                VisitQuota = visitQuota
            }, cancellationToken);

            return new CreateSubscriptionPackageResult(1, startDate, endDate);
        }

        if (request.VisitsCount <= 0)
        {
            throw new InvalidOperationException("VisitsCount must be greater than zero.");
        }

        if (request.DurationMinutes <= 0)
        {
            throw new InvalidOperationException("DurationMinutes must be greater than zero.");
        }

        var dayPattern = ResolvePattern(request.DayPattern);

        if (athlete.IsFullPackage)
        {
            athlete.IsFullPackage = false;
            await repository.UpdateAthleteAsync(athlete, cancellationToken);
        }

        var lanesCount = (await repository.GetLanesAsync(cancellationToken)).Count;
        if (lanesCount == 0)
        {
            throw new InvalidOperationException("No lanes configured.");
        }

        var existingSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var plannedDates = BuildPlannedDates(
            request.StartDateLocal.Date,
            request.VisitsCount,
            dayPattern,
            request.StartTimeLocal);
        var createdSchedules = new List<SubscriptionSchedule>();

        foreach (var date in plannedDates)
        {
            var preferred = request.PreferredLaneTypesByDayOfWeek.TryGetValue((int)date.DayOfWeek, out var p)
                ? p
                : PreferredLaneType.Any;

            if (athlete.Category == CustomerCategory.Amateur && preferred == PreferredLaneType.Long)
            {
                throw new InvalidOperationException("Həvəskar üçün yalnız qısa xətlər (1-8) mümkündür.");
            }

            var slotOccupancy = existingSchedules
                .Concat(createdSchedules)
                .Count(x =>
                    x.IsEnabled
                    && x.DayOfWeek == (int)date.DayOfWeek
                    && DateRangesOverlap(x.ActiveFromDateLocal.Date, x.ActiveToDateLocal.Date, date, date)
                    && TimeRangesOverlap(
                        x.StartTimeLocal,
                        x.DurationMinutes + 10,
                        request.StartTimeLocal,
                        request.DurationMinutes + 10)
                    && LaneTypeMatches(x.PreferredLaneType, preferred));

            if (slotOccupancy >= lanesCount)
            {
                throw new InvalidOperationException(
                    $"Təəssüf ki, seçdiyiniz saatda bütün {(preferred == PreferredLaneType.Long ? "Uzun" : preferred == PreferredLaneType.Short ? "Qısa" : "uyğun")} xətlər doludur. Zəhmət olmasa başqa vaxt seçin");
            }

            var created = await repository.AddSubscriptionScheduleAsync(new SubscriptionSchedule
            {
                AthleteId = athlete.Id,
                LaneNumber = 0,
                DayOfWeek = (int)date.DayOfWeek,
                StartTimeLocal = request.StartTimeLocal,
                DurationMinutes = request.DurationMinutes,
                ActiveFromDateLocal = date,
                ActiveToDateLocal = date,
                IsEnabled = true,
                PreferredLaneType = preferred,
                IsFullPackage = false
            }, cancellationToken);

            createdSchedules.Add(created);
        }

        return new CreateSubscriptionPackageResult(
            createdSchedules.Count,
            plannedDates.First(),
            plannedDates.Last());
    }

    private static IReadOnlyCollection<int> ResolvePattern(string dayPattern)
    {
        var normalized = dayPattern.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("DayPattern is required.");
        }

        // Allow custom patterns like "1,2,5" or "1-2-5"
        if (normalized.Any(char.IsDigit) && (normalized.Contains(',') || normalized.Contains('-') || normalized.Contains(' ')))
        {
            var parts = normalized
                .Replace(" ", "")
                .Split([',', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var days = parts
                .Select(p => int.TryParse(p, out var n) ? n : -1)
                .Where(n => n >= 0 && n <= 6)
                .Distinct()
                .ToArray();

            if (days.Length == 0)
            {
                throw new InvalidOperationException("DayPattern must include at least one day (0-6).");
            }

            return days;
        }

        return normalized switch
        {
            "1-3-5" or "135" or "odd" => OddDayPattern,
            "2-4-6" or "246" or "even" => EvenDayPattern,
            _ => throw new InvalidOperationException("DayPattern must be like 1-3-5, 2-4-6, or a custom list like 1,2,5.")
        };
    }

    private static List<DateTime> BuildPlannedDates(
        DateTime startDateLocal,
        int visitsCount,
        IReadOnlyCollection<int> dayPattern,
        TimeSpan startTimeLocal)
    {
        var results = new List<DateTime>(visitsCount);
        var cursor = startDateLocal;
        var nowLocal = AzerbaijanTime.NowLocal;

        for (var guard = 0; guard < 4000 && results.Count < visitsCount; guard++, cursor = cursor.AddDays(1))
        {
            if (!dayPattern.Contains((int)cursor.DayOfWeek))
            {
                continue;
            }

            if (SubscriptionOccurrenceRules.IsSlotInThePast(cursor, startTimeLocal, nowLocal))
            {
                continue;
            }

            results.Add(cursor);
        }

        if (results.Count != visitsCount)
        {
            throw new InvalidOperationException("Could not generate enough schedule dates for the selected day pattern.");
        }

        return results;
    }

    private static bool DateRangesOverlap(DateTime fromA, DateTime toA, DateTime fromB, DateTime toB)
    {
        return fromA <= toB && fromB <= toA;
    }

    private static bool TimeRangesOverlap(TimeSpan startA, int durationA, TimeSpan startB, int durationB)
    {
        var endA = startA.Add(TimeSpan.FromMinutes(durationA));
        var endB = startB.Add(TimeSpan.FromMinutes(durationB));
        return startA < endB && startB < endA;
    }

    private static bool LaneTypeMatches(PreferredLaneType existing, PreferredLaneType requested)
    {
        if (requested == PreferredLaneType.Any || existing == PreferredLaneType.Any)
        {
            return true;
        }
        return existing == requested;
    }
}
