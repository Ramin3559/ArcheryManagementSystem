using System.Globalization;
using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using EShooting.Web;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("info")]
public sealed class InfoController(ITrainingCenterRepository repository) : ControllerBase
{
    [HttpGet("athlete")]
    public async Task<IActionResult> GetAthleteInfo(
        [FromQuery] Guid? id,
        [FromQuery] string? phone,
        [FromQuery] string? email,
        [FromQuery] string? idCardNumber,
        CancellationToken cancellationToken)
    {
        Athlete? athlete = null;
        if (id is Guid athleteId && athleteId != Guid.Empty)
        {
            athlete = await repository.GetAthleteByIdAsync(athleteId, cancellationToken);
        }
        else
        {
            var phoneN = NormalizeDigits(phone);
            var emailN = NormalizeEmail(email);
            var idN = NormalizeText(idCardNumber);

            if (string.IsNullOrWhiteSpace(phoneN) && string.IsNullOrWhiteSpace(emailN) && string.IsNullOrWhiteSpace(idN))
            {
                return BadRequest(new { error = "Zəhmət olmasa bir məlumat daxil edin." });
            }

            var athletes = await repository.GetAthletesAsync(cancellationToken);
            athlete = athletes.FirstOrDefault(a =>
                (!string.IsNullOrWhiteSpace(phoneN) && NormalizeDigits(a.PhoneNumber) == phoneN)
                || (!string.IsNullOrWhiteSpace(emailN) && NormalizeEmail(a.Email) == emailN)
                || (!string.IsNullOrWhiteSpace(idN) && string.Equals(NormalizeText(a.IdCardNumber), idN, StringComparison.OrdinalIgnoreCase)));
        }

        if (athlete is null)
        {
            return NotFound(new { error = "Müştəri tapılmadı." });
        }

        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var schedules = (await repository.GetSubscriptionSchedulesAsync(cancellationToken)).ToList();
        var packageRecords = (await repository.GetCustomerPackageRecordsAsync(cancellationToken))
            .Where(r => r.AthleteId == athlete.Id)
            .ToList();
        var athleteSchedules = schedules.Where(s => s.AthleteId == athlete.Id).ToList();
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var laneNoById = lanes.ToDictionary(l => l.Id, l => l.Number);
        var staff = await repository.GetStaffMembersAsync(activeOnly: false, cancellationToken);
        var staffNameById = staff.ToDictionary(
            s => s.Id,
            s => $"{s.FirstName} {s.LastName}".Trim());
        var servicePackages = await repository.GetServicePackagesAsync(activeOnly: false, cancellationToken);
        var currentPackageScope = FacilityUsageRules.CurrentPackageScope(
            athlete.Id, packageRecords, servicePackages, athleteSchedules);
        var currentPackageName = FacilityUsageRules.CurrentPackageName(
            athlete.Id, packageRecords, servicePackages, athleteSchedules);

        var packages = schedules
            .Where(x => x.AthleteId == athlete.Id && x.IsEnabled)
            .GroupBy(x => new { From = x.ActiveFromDateLocal.Date, To = x.ActiveToDateLocal.Date, x.IsFullPackage })
            .OrderByDescending(g => g.Max(x => x.CreatedAtUtc))
            .Select(g =>
            {
                var from = g.Key.From;
                var to = g.Key.To;
                var dayLabels = g.Key.IsFullPackage
                    ? new List<string>
                    {
                        g.Any(FlexibleMonthlyRules.IsFlexibleMonthlySchedule)
                            ? "Aylıq sərbəst"
                            : "Limitsiz — istənilən vaxt gəliş"
                    }
                    : g
                        .Select(x => DayLabelAz(x.DayOfWeek))
                        .Distinct()
                        .ToList();

                return new
                {
                    fullName = athlete.FullName,
                    fromLocal = from.ToString("yyyy-MM-dd"),
                    toLocal = to.ToString("yyyy-MM-dd"),
                    days = string.Join(", ", dayLabels),
                    createdAtLocal = g.Max(x => x.CreatedAtUtc).ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    isFullPackage = g.Key.IsFullPackage
                };
            })
            .ToList();

        var activeSchedules = schedules.Where(s => s.AthleteId == athlete.Id && s.IsEnabled).ToList();
        var weeklySchedules = activeSchedules
            .Where(s => !s.IsFullPackage)
            .Select(s =>
            {
                var endTime = s.StartTimeLocal.Add(TimeSpan.FromMinutes(s.DurationMinutes));
                return new
                {
                    scheduleId = s.Id,
                    dayOfWeek = s.DayOfWeek,
                    dayLabel = DayLabelAz(s.DayOfWeek),
                    startTimeLocal = FormatTimeLocal(s.StartTimeLocal),
                    endTimeLocal = FormatTimeLocal(endTime),
                    durationMinutes = s.DurationMinutes,
                    laneNumber = s.LaneNumber,
                    laneLabel = s.LaneNumber > 0 ? $"Zolaq {s.LaneNumber}" : "—",
                    activeFromDateLocal = s.ActiveFromDateLocal.ToString("yyyy-MM-dd"),
                    activeToDateLocal = s.ActiveToDateLocal.ToString("yyyy-MM-dd"),
                    preferredLaneType = (int)s.PreferredLaneType,
                    isFullPackage = s.IsFullPackage
                };
            })
            .OrderBy(s => s.dayOfWeek)
            .ThenBy(s => s.startTimeLocal)
            .Cast<object>()
            .ToList();

        var occurrencesAll = BuildFlatOccurrences(athlete.FullName ?? "", activeSchedules);
        var todayIso = AzerbaijanTime.TodayLocal.ToString("yyyy-MM-dd");
        var remainingPlanned = occurrencesAll.Count(o =>
            o is OccurrenceRow row && string.CompareOrdinal(row.DateLocal, todayIso) >= 0);
        var visitStats = BuildVisitStats(
            athlete.Id,
            sessions,
            activeSchedules,
            athleteSchedules,
            packageRecords,
            remainingPlanned);

        var lastSessions = sessions
            .Where(x => x.AthleteId == athlete.Id)
            .Where(SessionActivationRules.CountsAsAttendedVisit)
            .OrderByDescending(x => x.StartTimeUtc)
            .Take(30)
            .Select(ses =>
            {
                var startLocal = AzerbaijanTime.UtcToLocalDateTime(ses.StartTimeUtc);
                var endLocal = AzerbaijanTime.UtcToLocalDateTime(ses.EndTimeUtc);
                return new
                {
                    dateLocal = startLocal.ToString("yyyy-MM-dd"),
                    dayLabel = DayLabelAz((int)startLocal.DayOfWeek),
                    startTime = $"{startLocal:HH:mm}",
                    endTime = $"{endLocal:HH:mm}",
                    durationHours = Math.Round((endLocal - startLocal).TotalMinutes / 60.0, 2),
                    packageTypeLabel = ResolveVisitPackageTypeLabel(ses, packageRecords, athleteSchedules),
                    facilityUsage = (int)FacilityUsageRules.Resolve(
                        ses.FacilityUsage,
                        laneNoById.TryGetValue(ses.LaneId, out var ln) ? ln : 0),
                    facilityUsageLabel = FacilityUsageRules.FormatVisitPlace(
                        laneNoById.TryGetValue(ses.LaneId, out var ln2) ? ln2 : 0,
                        ses.FacilityUsage),
                    handledByStaffName = ses.HandledByStaffId is Guid hid
                        && staffNameById.TryGetValue(hid, out var staffNm)
                        && !string.IsNullOrWhiteSpace(staffNm)
                            ? staffNm
                            : "—"
                };
            })
            .ToList();

        var visitedDates = lastSessions
            .Select(x => (string)x.dateLocal)
            .ToHashSet(StringComparer.Ordinal);
        var remainingCap = visitStats.Remaining;
        var occurrencesFlat = CapRemainingOccurrences(occurrencesAll, todayIso, visitedDates, remainingCap);

        return Ok(new
        {
            athleteId = athlete.Id,
            fullName = athlete.FullName,
            firstName = athlete.FirstName,
            lastName = athlete.LastName,
            phoneNumber = athlete.PhoneNumber,
            email = athlete.Email,
            idCardNumber = athlete.IdCardNumber,
            clubCardNumber = athlete.ClubCardNumber,
            clubCardType = athlete.ClubCardType,
            category = athlete.Category,
            membershipType = athlete.MembershipType,
            isSubscriber = athlete.IsSubscriber,
            isFullPackage = athlete.IsFullPackage,
            isVip = athlete.IsVip,
            isActive = athlete.IsActive,
            currentPackageName,
            currentPackageScope = currentPackageScope?.ToString(),
            currentPackageScopeLabel = FacilityUsageRules.ScopeLabel(currentPackageScope),
            requiresFacilityUsageChoice = currentPackageScope is PackageScope s
                && FacilityUsageRules.PackageRequiresVisitChoice(s),
            packages,
            weeklySchedules,
            occurrencesFlat = occurrencesFlat.Select(o => new
            {
                scheduleId = o.ScheduleId,
                athleteFullName = o.AthleteFullName,
                dateLocal = o.DateLocal,
                dayLabel = o.DayLabel,
                startTime = o.StartTime,
                endTime = o.EndTime,
                durationMinutes = o.DurationMinutes,
                laneNumber = o.LaneNumber,
                laneLabel = o.LaneLabel,
                preferredLaneType = o.PreferredLaneType,
                isFullPackage = o.IsFullPackage,
                isRescheduled = o.IsRescheduled,
                isMissed = string.CompareOrdinal(o.DateLocal, todayIso) < 0 && !visitedDates.Contains(o.DateLocal)
            }),
            visitStats,
            lastSessions
        });
    }

    private sealed record OccurrenceRow(
        Guid ScheduleId,
        string AthleteFullName,
        string DateLocal,
        string DayLabel,
        string StartTime,
        string EndTime,
        int DurationMinutes,
        int LaneNumber,
        string LaneLabel,
        int PreferredLaneType,
        bool IsFullPackage,
        bool IsRescheduled = false);

    private sealed record VisitStatsResult(
        int Visited,
        int OneTimeVisited,
        int MonthlyVisited,
        int? Remaining,
        string RemainingLabel,
        int? VisitLimit,
        int WeeklyDays,
        bool IsUnlimited,
        bool HasActiveSubscription,
        string? PeriodFromLocal,
        string? PeriodToLocal,
        string? MakeupDeadlineLocal,
        bool PeriodExpired,
        bool PackageEnded,
        bool HasCarryover);

    private static VisitStatsResult BuildVisitStats(
        Guid athleteId,
        IReadOnlyCollection<TrainingSession> sessions,
        List<SubscriptionSchedule> activeSchedules,
        List<SubscriptionSchedule> athleteSchedules,
        IReadOnlyList<CustomerPackageRecord> packageRecords,
        int remainingPlanned)
    {
        DateTime? periodFrom = null;
        DateTime? periodTo = null;
        if (activeSchedules.Count > 0)
        {
            periodFrom = activeSchedules.Min(s => s.ActiveFromDateLocal.Date);
            periodTo = activeSchedules.Max(s => s.ActiveToDateLocal.Date);
        }

        var enabledIds = activeSchedules.Select(s => s.Id).ToHashSet();
        var oneTimeVisited = 0;
        var monthlyVisited = 0;
        foreach (var session in sessions.Where(s => s.AthleteId == athleteId)
                     .Where(SessionActivationRules.CountsAsAttendedVisit))
        {
            var label = ResolveVisitPackageTypeLabel(session, packageRecords, athleteSchedules);
            if (IsOneTimeVisitLabel(label))
            {
                oneTimeVisited++;
                continue;
            }

            var day = AzerbaijanTime.UtcToLocalDate(session.StartTimeUtc);
            var onCurrentPlan = session.SubscriptionScheduleId is Guid sid && enabledIds.Contains(sid);
            var inPeriod = periodFrom is DateTime pf && day >= pf;
            if (onCurrentPlan || inPeriod)
            {
                monthlyVisited++;
            }
        }

        var fixedWeekly = activeSchedules.Where(s => !s.IsFullPackage).ToList();
        var flexibleMonthly = activeSchedules.Where(FlexibleMonthlyRules.IsFlexibleMonthlySchedule).ToList();
        var isUnlimited = activeSchedules.Any(s =>
            s.IsFullPackage && !FlexibleMonthlyRules.IsFlexibleMonthlySchedule(s));
        var today = AzerbaijanTime.TodayLocal;
        var visited = monthlyVisited;

        int? visitLimit = null;
        int? remaining = null;
        string remainingLabel;
        var weeklyDays = fixedWeekly.Select(s => s.DayOfWeek).Distinct().Count();
        var periodExpired = periodTo is DateTime pto && today > pto;
        var packageEnded = false;
        var hasCarryover = false;

        if (activeSchedules.Count == 0)
        {
            remainingLabel = "—";
        }
        else if (isUnlimited)
        {
            remainingLabel = "Limitsiz";
        }
        else if (flexibleMonthly.Count > 0 && periodTo is DateTime flexTo)
        {
            visitLimit = flexibleMonthly.Max(s => s.VisitQuota ?? 0);
            remaining = Math.Max(0, visitLimit.Value - visited);
            remainingLabel = remaining.Value.ToString();
            var makeupDeadline = WeeklyVisitPeriodRules.MakeupDeadline(flexTo);
            var makeupOpen = today <= makeupDeadline;
            hasCarryover = periodExpired && remaining.Value > 0 && makeupOpen;
            packageEnded = remaining.Value <= 0 || !makeupOpen;
        }
        else if (fixedWeekly.Count > 0 && periodFrom is DateTime pf && periodTo is DateTime pt)
        {
            visitLimit = WeeklyVisitPeriodRules.ResolveVisitLimit(
                fixedWeekly.Select(s => (
                    s.DayOfWeek,
                    (IReadOnlySet<string>)OccurrenceJson.DeserializeExcluded(s.ExcludedOccurrenceDatesJson))),
                pf,
                pt,
                weeklyDays);

            remaining = Math.Max(0, visitLimit.Value - visited);
            remainingLabel = remaining.Value.ToString();
            var makeupDeadline = WeeklyVisitPeriodRules.MakeupDeadline(pt);
            var makeupOpen = today <= makeupDeadline;
            hasCarryover = periodExpired && remaining.Value > 0 && makeupOpen;
            packageEnded = remaining.Value <= 0 || !makeupOpen;
        }
        else
        {
            remaining = remainingPlanned;
            remainingLabel = remainingPlanned.ToString();
        }

        DateTime? makeupDeadlineLocal = null;
        if ((fixedWeekly.Count > 0 || flexibleMonthly.Count > 0) && periodTo is DateTime pt2)
        {
            makeupDeadlineLocal = WeeklyVisitPeriodRules.MakeupDeadline(pt2);
        }

        return new VisitStatsResult(
            visited,
            oneTimeVisited,
            monthlyVisited,
            remaining,
            remainingLabel,
            visitLimit,
            weeklyDays,
            isUnlimited,
            activeSchedules.Count > 0,
            periodFrom?.ToString("yyyy-MM-dd"),
            periodTo?.ToString("yyyy-MM-dd"),
            makeupDeadlineLocal?.ToString("yyyy-MM-dd"),
            periodExpired,
            packageEnded,
            hasCarryover);
    }

    private static List<OccurrenceRow> CapRemainingOccurrences(
        List<OccurrenceRow> all,
        string todayIso,
        HashSet<string> visitedDates,
        int? remaining)
    {
        if (remaining is null)
        {
            return all;
        }

        var ordered = all
            .OrderBy(o => o.DateLocal, StringComparer.Ordinal)
            .ThenBy(o => o.StartTime, StringComparer.Ordinal)
            .ToList();
        var missed = ordered.Where(o =>
            string.CompareOrdinal(o.DateLocal, todayIso) < 0
            && !visitedDates.Contains(o.DateLocal));
        var upcoming = ordered.Where(o => string.CompareOrdinal(o.DateLocal, todayIso) >= 0);
        return missed.Concat(upcoming).Take(Math.Max(0, remaining.Value)).ToList();
    }

    private static bool IsOneTimeVisitLabel(string? label)
    {
        var t = (label ?? "").Trim();
        if (t.Length == 0)
        {
            return false;
        }

        return t.Contains("Birdəfəlik", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Birdefəlik", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Birdefelik", StringComparison.OrdinalIgnoreCase)
            || t.Contains("OneTime", StringComparison.OrdinalIgnoreCase);
    }

    private static List<OccurrenceRow> BuildFlatOccurrences(string athleteFullName, List<SubscriptionSchedule> schedules)
    {
        var temp = new List<(string dateKey, string startKey, OccurrenceRow row)>();
        foreach (var s in schedules)
        {
            if (s.IsFullPackage) continue;

            var excluded = OccurrenceJson.DeserializeExcluded(s.ExcludedOccurrenceDatesJson);
            var overrides = OccurrenceJson.OverridesToMap(OccurrenceJson.DeserializeOverrides(s.OccurrenceOverridesJson));
            var from = s.ActiveFromDateLocal.Date;
            var to = s.ActiveToDateLocal.Date;
            var addedDates = new HashSet<string>(StringComparer.Ordinal);
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if ((int)day.DayOfWeek != s.DayOfWeek) continue;
                var dateKey = day.ToString("yyyy-MM-dd");
                if (excluded.Contains(dateKey)) continue;

                var start = s.StartTimeLocal;
                var dur = s.DurationMinutes;
                var lane = s.LaneNumber;
                if (overrides.TryGetValue(dateKey, out var ov))
                {
                    if (!string.IsNullOrWhiteSpace(ov.StartTimeLocal) && TimeSpan.TryParse(ov.StartTimeLocal, out var st))
                        start = st;
                    if (ov.DurationMinutes is > 0)
                        dur = ov.DurationMinutes.Value;
                    if (ov.LaneNumber is > 0)
                        lane = ov.LaneNumber.Value;
                }

                var endT = start.Add(TimeSpan.FromMinutes(dur));
                var laneLabel = lane > 0 ? $"Zolaq {lane}" : "Zolaq təyin edilməyib";
                var startKey = FormatTimeLocal(start);
                var endKey = FormatTimeLocal(endT);
                var row = new OccurrenceRow(
                    s.Id,
                    athleteFullName,
                    dateKey,
                    DayLabelAz(s.DayOfWeek),
                    startKey,
                    endKey,
                    dur,
                    lane,
                    laneLabel,
                    (int)s.PreferredLaneType,
                    s.IsFullPackage);
                temp.Add((dateKey, startKey, row));
                addedDates.Add(dateKey);
            }

            foreach (var kv in overrides)
            {
                var dateKey = kv.Key?.Trim();
                if (string.IsNullOrWhiteSpace(dateKey) || excluded.Contains(dateKey) || addedDates.Contains(dateKey))
                {
                    continue;
                }

                if (!DateTime.TryParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var rescheduledDay))
                {
                    continue;
                }

                if (rescheduledDay.Date < from || rescheduledDay.Date > to)
                {
                    continue;
                }

                var ov = kv.Value;
                var start = s.StartTimeLocal;
                var dur = s.DurationMinutes;
                var lane = s.LaneNumber;
                if (!string.IsNullOrWhiteSpace(ov.StartTimeLocal) && TimeSpan.TryParse(ov.StartTimeLocal, out var st))
                {
                    start = st;
                }

                if (ov.DurationMinutes is > 0)
                {
                    dur = ov.DurationMinutes.Value;
                }

                if (ov.LaneNumber is > 0)
                {
                    lane = ov.LaneNumber.Value;
                }

                var endT = start.Add(TimeSpan.FromMinutes(dur));
                var laneLabel = lane > 0 ? $"Zolaq {lane}" : "Zolaq təyin edilməyib";
                var startKey = FormatTimeLocal(start);
                var endKey = FormatTimeLocal(endT);
                var row = new OccurrenceRow(
                    s.Id,
                    athleteFullName,
                    dateKey,
                    DayLabelAz((int)rescheduledDay.DayOfWeek),
                    startKey,
                    endKey,
                    dur,
                    lane,
                    laneLabel,
                    (int)s.PreferredLaneType,
                    s.IsFullPackage,
                    IsRescheduled: true);
                temp.Add((dateKey, startKey, row));
                addedDates.Add(dateKey);
            }
        }

        return temp
            .OrderBy(x => x.dateKey, StringComparer.Ordinal)
            .ThenBy(x => x.startKey, StringComparer.Ordinal)
            .Select(x => x.row)
            .ToList();
    }

    private static string ResolveVisitPackageTypeLabel(
        TrainingSession session,
        IReadOnlyList<CustomerPackageRecord> packageRecords,
        IReadOnlyList<SubscriptionSchedule> athleteSchedules)
    {
        var bySession = packageRecords.FirstOrDefault(r => r.SessionId == session.Id);
        if (bySession is not null)
        {
            return FormatPackageTypeLabel(bySession);
        }

        if (session.SubscriptionScheduleId is Guid scheduleId && scheduleId != Guid.Empty)
        {
            var byScheduleRecord = packageRecords
                .Where(r => r.SubscriptionScheduleId == scheduleId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();
            if (byScheduleRecord is not null)
            {
                return FormatPackageTypeLabel(byScheduleRecord);
            }

            var schedule = athleteSchedules.FirstOrDefault(s => s.Id == scheduleId);
            if (schedule is not null)
            {
                return schedule.IsFullPackage ? "Limitsiz" : "Abunə";
            }
        }

        var day = AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(session.StartTimeUtc));
        var covering = athleteSchedules
            .Where(s => s.ActiveFromDateLocal.Date <= day && s.ActiveToDateLocal.Date >= day)
            .OrderByDescending(s => s.IsEnabled)
            .ThenByDescending(s => s.CreatedAtUtc)
            .FirstOrDefault();
        if (covering is not null)
        {
            var recNear = packageRecords
                .Where(r => r.CreatedAtUtc <= DateTimeAssumedUtc.AsUtc(session.StartTimeUtc).AddHours(12))
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();
            if (recNear is not null
                && (recNear.SubscriptionScheduleId is null
                    || covering.Id == recNear.SubscriptionScheduleId
                    || !string.IsNullOrWhiteSpace(recNear.PackageName)))
            {
                var label = FormatPackageTypeLabel(recNear);
                if (!string.IsNullOrWhiteSpace(label) && label != "—")
                {
                    return label;
                }
            }

            return covering.IsFullPackage ? "Limitsiz" : "Abunə";
        }

        var oneTimeNear = packageRecords
            .Where(r => r.SessionId == session.Id
                || (r.CreatedAtUtc <= DateTimeAssumedUtc.AsUtc(session.StartTimeUtc).AddHours(1)
                    && r.CreatedAtUtc >= DateTimeAssumedUtc.AsUtc(session.StartTimeUtc).AddHours(-6)))
            .OrderByDescending(r => r.SessionId == session.Id)
            .ThenByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault();
        if (oneTimeNear is not null)
        {
            return FormatPackageTypeLabel(oneTimeNear);
        }

        return "Birdefəlik";
    }

    private static string FormatPackageTypeLabel(CustomerPackageRecord record)
    {
        var name = (record.PackageName ?? "").Trim();
        var billing = (record.BillingTypeLabel ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (!string.IsNullOrWhiteSpace(billing))
        {
            return billing;
        }

        return "—";
    }

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var chars = value.Where(char.IsDigit).ToArray();
        return new string(chars);
    }

    private static string NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
    }

    private static string FormatTimeLocal(TimeSpan time)
    {
        var normalized = time - TimeSpan.FromDays(time.Days);
        return $"{normalized.Hours:D2}:{normalized.Minutes:D2}";
    }

    private static string DayLabelAz(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            1 => "B.e",
            2 => "Ç.a",
            3 => "Ç",
            4 => "C.a",
            5 => "C",
            6 => "Ş",
            0 => "B",
            _ => "—"
        };
    }
}
