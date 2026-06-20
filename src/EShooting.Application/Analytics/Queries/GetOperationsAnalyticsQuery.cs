using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Analytics.Queries;

public sealed record GetOperationsAnalyticsQuery(
    DateTime FromLocalDate,
    DateTime ToLocalDate,
    string Mode = "range") : IRequest<OperationsAnalyticsResult>;

public sealed class GetOperationsAnalyticsQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetOperationsAnalyticsQuery, OperationsAnalyticsResult>
{
    public async Task<OperationsAnalyticsResult> Handle(
        GetOperationsAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.FromLocalDate.Date;
        var to = request.ToLocalDate.Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var issues = await repository.GetSessionEquipmentIssuesAsync(cancellationToken);
        var equipmentItems = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);

        var equipmentById = equipmentItems.ToDictionary(x => x.Id);
        var laneNoById = lanes.ToDictionary(x => x.Id, x => x.Number);
        var laneNumbers = lanes.Select(x => x.Number).Distinct().OrderBy(x => x).ToList();
        if (laneNumbers.Count == 0)
        {
            laneNumbers = Enumerable.Range(1, 11).ToList();
        }

        var firstSessionDayByAthlete = BuildFirstSessionDayByAthlete(sessions);

        var rangeSessions = sessions
            .Where(s => IsSessionInLocalRange(s, from, to))
            .ToList();

        var rangeSessionIds = rangeSessions.Select(s => s.Id).ToHashSet();
        var rangeIssues = issues
            .Where(i => rangeSessionIds.Contains(i.SessionId))
            .ToList();

        var rangeSubscriptions = schedules
            .Where(s => IsUtcInLocalRange(s.CreatedAtUtc, from, to))
            .ToList();

        decimal PriceFor(Guid equipmentItemId)
            => equipmentById.GetValueOrDefault(equipmentItemId)?.Price ?? 0m;

        string NameFor(Guid equipmentItemId)
            => equipmentById.GetValueOrDefault(equipmentItemId)?.Name ?? "Avadanlıq";

        var equipmentBreakdown = rangeIssues
            .GroupBy(x => x.EquipmentItemId)
            .Select(g =>
            {
                var sales = g.Where(x => x.IssueType == EquipmentIssueType.Sale).ToList();
                var rentals = g.Where(x => x.IssueType == EquipmentIssueType.Rental).ToList();
                var price = PriceFor(g.Key);
                return new EquipmentAnalyticsRow
                {
                    EquipmentName = NameFor(g.Key),
                    SaleCount = sales.Count,
                    RentalCount = rentals.Count,
                    SaleRevenue = sales.Count * price,
                    RentalRevenue = rentals.Count * price
                };
            })
            .OrderByDescending(x => x.SaleCount + x.RentalCount)
            .ThenBy(x => x.EquipmentName)
            .ToList();

        var laneActivity = BuildLaneActivity(rangeSessions, laneNoById, laneNumbers);
        var dailyBreakdown = BuildDailyBreakdown(
            from,
            to,
            rangeSessions,
            rangeIssues,
            rangeSubscriptions,
            firstSessionDayByAthlete,
            PriceFor);

        var uniqueAthleteIds = rangeSessions
            .Select(s => s.AthleteId)
            .Distinct()
            .Count();

        var newCustomerCount = rangeSessions
            .Select(s => s.AthleteId)
            .Distinct()
            .Count(athleteId =>
            {
                if (!firstSessionDayByAthlete.TryGetValue(athleteId, out var firstDay))
                {
                    return false;
                }

                return firstDay >= from && firstDay <= to;
            });

        var saleIssues = rangeIssues.Where(x => x.IssueType == EquipmentIssueType.Sale).ToList();
        var rentalIssues = rangeIssues.Where(x => x.IssueType == EquipmentIssueType.Rental).ToList();
        var totalLaneHours = RoundHours(laneActivity.Sum(x => x.TotalHours));
        var busiestLane = laneActivity
            .Where(x => x.SessionCount > 0 || x.TotalHours > 0)
            .OrderByDescending(x => x.TotalHours)
            .ThenByDescending(x => x.SessionCount)
            .FirstOrDefault();

        return new OperationsAnalyticsResult
        {
            Mode = request.Mode,
            FromLocal = from.ToString("yyyy-MM-dd"),
            ToLocal = to.ToString("yyyy-MM-dd"),
            Label = from == to ? from.ToString("yyyy-MM-dd") : $"{from:yyyy-MM-dd} — {to:yyyy-MM-dd}",
            SessionCount = rangeSessions.Count,
            UniqueCustomerCount = uniqueAthleteIds,
            NewCustomerCount = newCustomerCount,
            SubscriptionCreatedCount = rangeSubscriptions.Count,
            EquipmentSaleCount = saleIssues.Count,
            EquipmentRentalCount = rentalIssues.Count,
            EquipmentSaleRevenue = saleIssues.Sum(x => PriceFor(x.EquipmentItemId)),
            EquipmentRentalRevenue = rentalIssues.Sum(x => PriceFor(x.EquipmentItemId)),
            TotalLaneHours = totalLaneHours,
            BusiestLaneNumber = busiestLane?.LaneNumber,
            DailyBreakdown = dailyBreakdown,
            LaneActivity = laneActivity,
            EquipmentBreakdown = equipmentBreakdown
        };
    }

    private static List<DailyOperationsRow> BuildDailyBreakdown(
        DateTime from,
        DateTime to,
        IReadOnlyCollection<Domain.Entities.TrainingSession> rangeSessions,
        IReadOnlyCollection<Domain.Entities.SessionEquipmentIssue> rangeIssues,
        IReadOnlyCollection<Domain.Entities.SubscriptionSchedule> rangeSubscriptions,
        IReadOnlyDictionary<Guid, DateTime> firstSessionDayByAthlete,
        Func<Guid, decimal> priceFor)
    {
        var rows = new List<DailyOperationsRow>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var daySessions = rangeSessions
                .Where(s => ToLocalDate(StartUtc(s)) == day)
                .ToList();
            var daySessionIds = daySessions.Select(s => s.Id).ToHashSet();
            var dayIssues = rangeIssues.Where(i => daySessionIds.Contains(i.SessionId)).ToList();
            var daySubscriptions = rangeSubscriptions
                .Where(s => ToLocalDate(AssumedUtc(s.CreatedAtUtc)) == day)
                .ToList();

            var uniqueAthletes = daySessions.Select(s => s.AthleteId).Distinct().ToList();
            var newCustomers = uniqueAthletes.Count(athleteId =>
                firstSessionDayByAthlete.TryGetValue(athleteId, out var firstDay) && firstDay == day);

            var sales = dayIssues.Where(x => x.IssueType == EquipmentIssueType.Sale).ToList();
            var rentals = dayIssues.Where(x => x.IssueType == EquipmentIssueType.Rental).ToList();
            var laneHours = daySessions.Sum(s => SessionDurationHours(s));

            rows.Add(new DailyOperationsRow
            {
                DateLocal = day.ToString("yyyy-MM-dd"),
                SessionCount = daySessions.Count,
                UniqueCustomerCount = uniqueAthletes.Count,
                NewCustomerCount = newCustomers,
                SubscriptionCreatedCount = daySubscriptions.Count,
                EquipmentSaleCount = sales.Count,
                EquipmentRentalCount = rentals.Count,
                EquipmentSaleRevenue = sales.Sum(x => priceFor(x.EquipmentItemId)),
                EquipmentRentalRevenue = rentals.Sum(x => priceFor(x.EquipmentItemId)),
                LaneHoursTotal = RoundHours(laneHours)
            });
        }

        return rows;
    }

    private static List<LaneActivityRow> BuildLaneActivity(
        IReadOnlyCollection<Domain.Entities.TrainingSession> rangeSessions,
        IReadOnlyDictionary<Guid, int> laneNoById,
        IReadOnlyList<int> laneNumbers)
    {
        var count = laneNumbers.ToDictionary(n => n, _ => 0);
        var hours = laneNumbers.ToDictionary(n => n, _ => 0.0);

        foreach (var session in rangeSessions)
        {
            if (!laneNoById.TryGetValue(session.LaneId, out var laneNo) || laneNo <= 0)
            {
                continue;
            }

            if (!count.ContainsKey(laneNo))
            {
                count[laneNo] = 0;
                hours[laneNo] = 0;
            }

            count[laneNo]++;
            hours[laneNo] += SessionDurationHours(session);
        }

        return laneNumbers
            .Select(n => new LaneActivityRow
            {
                LaneNumber = n,
                SessionCount = count.GetValueOrDefault(n),
                TotalHours = RoundHours(hours.GetValueOrDefault(n))
            })
            .ToList();
    }

    private static Dictionary<Guid, DateTime> BuildFirstSessionDayByAthlete(
        IReadOnlyCollection<Domain.Entities.TrainingSession> sessions)
    {
        var map = new Dictionary<Guid, DateTime>();
        foreach (var session in sessions)
        {
            var day = ToLocalDate(StartUtc(session));
            if (!map.TryGetValue(session.AthleteId, out var existing) || day < existing)
            {
                map[session.AthleteId] = day;
            }
        }

        return map;
    }

    private static bool IsSessionInLocalRange(
        Domain.Entities.TrainingSession session,
        DateTime fromLocalDate,
        DateTime toLocalDate)
    {
        var day = ToLocalDate(StartUtc(session));
        return day >= fromLocalDate && day <= toLocalDate;
    }

    private static bool IsUtcInLocalRange(DateTime utcValue, DateTime fromLocalDate, DateTime toLocalDate)
    {
        var day = ToLocalDate(AssumedUtc(utcValue));
        return day >= fromLocalDate && day <= toLocalDate;
    }

    private static double SessionDurationHours(Domain.Entities.TrainingSession session)
    {
        var start = StartUtc(session);
        var end = AssumedUtc(session.EndTimeUtc);
        if (end <= start)
        {
            return 0;
        }

        return Math.Max(0, (end - start).TotalHours);
    }

    private static DateTime StartUtc(Domain.Entities.TrainingSession session)
        => AssumedUtc(session.StartTimeUtc);

    private static DateTime AssumedUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static DateTime ToLocalDate(DateTime utc) => utc.ToLocalTime().Date;

    private static double RoundHours(double h) => Math.Round(h, 2);
}
