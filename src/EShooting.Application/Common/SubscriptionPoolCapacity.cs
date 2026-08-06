using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

/// <summary>
/// Aylıq abunə tutumu: konkret zolaq əvəzinə qrup (1–8 / 9–11 / bütün zolaqlar üzrə).
/// </summary>
public readonly record struct SubscriptionPoolSnapshot(int ShortUsed, int LongUsed, int AnyUsed)
{
    public const int ShortCapacity = 8;
    public const int LongCapacity = 3;
    public const int TotalCapacity = ShortCapacity + LongCapacity;

    public int TotalUsed => ShortUsed + LongUsed + AnyUsed;

    public bool IsFeasible()
        => ShortUsed <= ShortCapacity
           && LongUsed <= LongCapacity
           && TotalUsed <= TotalCapacity;

    public bool CanFit(PreferredLaneType requested)
    {
        var next = requested switch
        {
            PreferredLaneType.Short => this with { ShortUsed = ShortUsed + 1 },
            PreferredLaneType.Long => this with { LongUsed = LongUsed + 1 },
            _ => this with { AnyUsed = AnyUsed + 1 }
        };
        return next.IsFeasible();
    }

    public string FormatAz()
        => $"1–8: {ShortUsed}/{ShortCapacity} · 9–11: {LongUsed}/{LongCapacity} · Bütün zolaqlar üzrə: {AnyUsed} · Ümumi: {TotalUsed}/{TotalCapacity}";
}

public static class SubscriptionPoolCapacity
{
    public static PreferredLaneType NormalizeForAthlete(CustomerCategory category, PreferredLaneType preferred)
    {
        if (category == CustomerCategory.Amateur)
        {
            return PreferredLaneType.Short;
        }

        return preferred;
    }

    /// <summary>
    /// Konkret zolaq (schedule və ya override) varsa ona görə; yoxsa PreferredLaneType.
    /// LastAssignedLaneNumber tutuma düşmür — o, keçmiş gedişdir.
    /// </summary>
    public static PreferredLaneType ClassifyDemand(
        SubscriptionSchedule schedule,
        DateTime dayLocal)
    {
        var explicitLane = ResolveExplicitLaneNumber(schedule, dayLocal);
        if (explicitLane is >= 1 and <= 8)
        {
            return PreferredLaneType.Short;
        }

        if (explicitLane is >= 9 and <= 11)
        {
            return PreferredLaneType.Long;
        }

        return schedule.PreferredLaneType switch
        {
            PreferredLaneType.Short => PreferredLaneType.Short,
            PreferredLaneType.Long => PreferredLaneType.Long,
            _ => PreferredLaneType.Any
        };
    }

    public static int ResolveExplicitLaneNumber(SubscriptionSchedule schedule, DateTime dayLocal)
    {
        if (schedule.LaneNumber is >= 1 and <= 11)
        {
            return schedule.LaneNumber;
        }

        var key = dayLocal.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var ov = SubscriptionOccurrenceJson.DeserializeOverrides(schedule.OccurrenceOverridesJson)
            .FirstOrDefault(o => string.Equals(o.DateLocal?.Trim(), key, StringComparison.Ordinal));
        if (ov?.LaneNumber is >= 1 and <= 11)
        {
            return ov.LaneNumber.Value;
        }

        return 0;
    }

    /// <summary>
    /// Aylıq pool planı — konkret zolaq hələ seçilməyib («Seçiləcək»).
    /// DB-də müvəqqəti LaneId ola bilər, amma zolağı tutmur.
    /// </summary>
    public static bool IsUnassignedPoolSession(
        TrainingSession session,
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        DateTime dayLocal)
    {
        if (SessionActivationRules.HasActivation(session) || session.Status == SessionStatus.Completed)
        {
            return false;
        }

        if (session.SubscriptionScheduleId is not Guid sid)
        {
            return false;
        }

        var schedule = schedules.FirstOrDefault(s => s.Id == sid);
        if (schedule is null)
        {
            return false;
        }

        return ResolveExplicitLaneNumber(schedule, dayLocal) <= 0;
    }

    public static SubscriptionPoolSnapshot CountForSlot(
        IReadOnlyCollection<SubscriptionSchedule> schedules,
        DateTime dayLocal,
        TimeSpan startTimeLocal,
        int durationMinutes,
        Guid? excludeScheduleId = null)
    {
        if (durationMinutes <= 0)
        {
            return default;
        }

        var reqStart = dayLocal.Date.Add(startTimeLocal);
        var reqEnd = reqStart.AddMinutes(durationMinutes);
        var shortUsed = 0;
        var longUsed = 0;
        var anyUsed = 0;

        foreach (var schedule in schedules)
        {
            if (excludeScheduleId is Guid exId && schedule.Id == exId)
            {
                continue;
            }

            if (!SubscriptionOccurrenceJson.TryResolveOccurrence(
                    schedule,
                    dayLocal,
                    out var otherStart,
                    out var otherDuration,
                    out _))
            {
                continue;
            }

            if (otherDuration <= 0)
            {
                continue;
            }

            var otherStartLocal = dayLocal.Date.Add(otherStart);
            var otherEndLocal = otherStartLocal.AddMinutes(otherDuration);
            if (!(reqStart < otherEndLocal && reqEnd > otherStartLocal))
            {
                continue;
            }

            switch (ClassifyDemand(schedule, dayLocal))
            {
                case PreferredLaneType.Short:
                    shortUsed++;
                    break;
                case PreferredLaneType.Long:
                    longUsed++;
                    break;
                default:
                    anyUsed++;
                    break;
            }
        }

        return new SubscriptionPoolSnapshot(shortUsed, longUsed, anyUsed);
    }

    public static string BusyMessage(PreferredLaneType requested, SubscriptionPoolSnapshot snapshot)
    {
        var group = requested switch
        {
            PreferredLaneType.Short => "1–8 zolaqları",
            PreferredLaneType.Long => "9–11 zolaqları",
            _ => "bütün zolaqlar üzrə"
        };
        return $"Təəssüf ki, seçdiyiniz saatda {group} doludur ({snapshot.FormatAz()}). Zəhmət olmasa başqa vaxt seçin.";
    }
}
