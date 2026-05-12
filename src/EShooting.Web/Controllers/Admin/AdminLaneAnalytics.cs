using EShooting.Domain.Entities;

namespace EShooting.Web.Controllers.Admin;

/// <summary>
/// Admin dashboard üçün zolaq üzrə sessiya sayı və faktiki müddət (saat) hesablamaları.
/// Bir sessiya seçilmiş aralığa daxil olunur: başlanğıc vaxtının yerli <c>Tarix</c> hissəsi aralıqda olmalıdır.
/// </summary>
public static class AdminLaneAnalytics
{
    public sealed record LaneStatRow(int LaneNumber, int SessionCount, double TotalHours);

    public sealed record RangeResult(
        string Mode,
        string FromLocal,
        string ToLocal,
        string Label,
        IReadOnlyList<LaneStatRow> ByLane,
        int? BusiestLaneNumber,
        int SessionTotal,
        double HoursTotal);

    /// <summary>
    /// Cari gün üçün sürətli icmal qaytarır (default ilkin yüklənmə üçün).
    /// </summary>
    public static RangeResult ComputeToday(IReadOnlyCollection<TrainingSession> sessions, IReadOnlyCollection<Lane> lanes)
    {
        var today = DateTime.Now.Date;
        return Compute(sessions, lanes, today, today, mode: "today");
    }

    public static RangeResult Compute(
        IReadOnlyCollection<TrainingSession> sessions,
        IReadOnlyCollection<Lane> lanes,
        DateTime fromLocalDate,
        DateTime toLocalDate,
        string mode = "range")
    {
        var laneNumbers = lanes.Select(l => l.Number).Distinct().OrderBy(n => n).ToList();
        if (laneNumbers.Count == 0)
        {
            laneNumbers = Enumerable.Range(1, 11).ToList();
        }

        var laneNoById = lanes.ToDictionary(l => l.Id, l => l.Number);

        var from = fromLocalDate.Date;
        var to = toLocalDate.Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var rows = BuildRows(sessions, laneNoById, laneNumbers, from, to);

        return new RangeResult(
            Mode: mode,
            FromLocal: from.ToString("yyyy-MM-dd"),
            ToLocal: to.ToString("yyyy-MM-dd"),
            Label: from == to ? from.ToString("yyyy-MM-dd") : $"{from:yyyy-MM-dd} — {to:yyyy-MM-dd}",
            ByLane: rows,
            BusiestLaneNumber: PickBusiest(rows),
            SessionTotal: rows.Sum(r => r.SessionCount),
            HoursTotal: RoundHours(rows.Sum(r => r.TotalHours)));
    }

    private static IReadOnlyList<LaneStatRow> BuildRows(
        IReadOnlyCollection<TrainingSession> sessions,
        IReadOnlyDictionary<Guid, int> laneNoById,
        List<int> laneNumbers,
        DateTime fromLocalDate,
        DateTime toLocalDate)
    {
        var count = laneNumbers.ToDictionary(n => n, _ => 0);
        var hours = laneNumbers.ToDictionary(n => n, _ => 0.0);

        foreach (var s in sessions)
        {
            if (!laneNoById.TryGetValue(s.LaneId, out var laneNo) || laneNo <= 0)
            {
                continue;
            }

            var startLocal = ToLocalDate(StartUtc(s));
            if (startLocal < fromLocalDate.Date || startLocal > toLocalDate.Date)
            {
                continue;
            }

            if (!count.ContainsKey(laneNo))
            {
                count[laneNo] = 0;
                hours[laneNo] = 0;
            }

            count[laneNo]++;
            hours[laneNo] += SessionDurationHours(s);
        }

        return laneNumbers
            .Select(n => new LaneStatRow(n, count.GetValueOrDefault(n), RoundHours(hours.GetValueOrDefault(n))))
            .ToList();
    }

    private static int? PickBusiest(IReadOnlyList<LaneStatRow> rows)
    {
        var best = rows
            .OrderByDescending(r => r.TotalHours)
            .ThenByDescending(r => r.SessionCount)
            .FirstOrDefault();
        if (best is null || (best.SessionCount == 0 && best.TotalHours <= 0))
        {
            return null;
        }

        return best.LaneNumber;
    }

    private static double SessionDurationHours(TrainingSession s)
    {
        var su = StartUtc(s);
        var eu = EndUtc(s);
        if (eu <= su)
        {
            return 0;
        }

        return Math.Max(0, (eu - su).TotalHours);
    }

    private static DateTime StartUtc(TrainingSession s) => AssumedUtc(s.StartTimeUtc);

    private static DateTime EndUtc(TrainingSession s) => AssumedUtc(s.EndTimeUtc);

    private static DateTime AssumedUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static DateTime ToLocalDate(DateTime utc) => utc.ToLocalTime().Date;

    private static double RoundHours(double h) => Math.Round(h, 2);
}
