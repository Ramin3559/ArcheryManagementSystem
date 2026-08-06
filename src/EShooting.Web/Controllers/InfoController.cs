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

        var packages = schedules
            .Where(x => x.AthleteId == athlete.Id && x.IsEnabled)
            .GroupBy(x => new { From = x.ActiveFromDateLocal.Date, To = x.ActiveToDateLocal.Date, x.IsFullPackage })
            .OrderByDescending(g => g.Max(x => x.CreatedAtUtc))
            .Select(g =>
            {
                var from = g.Key.From;
                var to = g.Key.To;
                var dayLabels = g.Key.IsFullPackage
                    ? new List<string> { "Limitsiz — istənilən vaxt gəliş" }
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

        var occurrencesFlat = BuildFlatOccurrences(athlete.FullName ?? "", activeSchedules);
        var todayIso = AzerbaijanTime.TodayLocal.ToString("yyyy-MM-dd");
        var remainingPlanned = occurrencesFlat.Count(o =>
            o is OccurrenceRow row && string.CompareOrdinal(row.DateLocal, todayIso) >= 0);
        var visitStats = BuildVisitStats(athlete.Id, sessions, activeSchedules, remainingPlanned);

        var lastSessions = sessions
            .Where(x => x.AthleteId == athlete.Id)
            .Where(x => x.Status is SessionStatus.Active or SessionStatus.Completed)
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
                    durationHours = Math.Round((endLocal - startLocal).TotalMinutes / 60.0, 2)
                };
            })
            .ToList();

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
                isRescheduled = o.IsRescheduled
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

    private static object BuildVisitStats(
        Guid athleteId,
        IReadOnlyCollection<TrainingSession> sessions,
        List<SubscriptionSchedule> activeSchedules,
        int remainingPlanned)
    {
        DateTime? periodFrom = null;
        DateTime? periodTo = null;
        if (activeSchedules.Count > 0)
        {
            periodFrom = activeSchedules.Min(s => s.ActiveFromDateLocal.Date);
            periodTo = activeSchedules.Max(s => s.ActiveToDateLocal.Date);
        }

        var athleteSessions = sessions
            .Where(s => s.AthleteId == athleteId)
            .Where(s => s.Status is SessionStatus.Active or SessionStatus.Completed)
            .Select(s => AzerbaijanTime.UtcToLocalDate(s.StartTimeUtc))
            .ToList();

        var fixedWeekly = activeSchedules.Where(s => !s.IsFullPackage).ToList();
        var isUnlimited = activeSchedules.Any(s => s.IsFullPackage);
        var today = AzerbaijanTime.TodayLocal;

        int visited;
        if (periodFrom is DateTime from)
        {
            // Dövr başlanğıcından indiyə qədər (qalıq gediş üçün bitmə tarixindən sonra da sayılır).
            visited = athleteSessions.Count(d => d >= from);
        }
        else
        {
            visited = athleteSessions.Count;
        }

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
        else if (fixedWeekly.Count > 0 && periodFrom is DateTime pf && periodTo is DateTime pt)
        {
            var months = Math.Max(1, ((pt.Year - pf.Year) * 12) + (pt.Month - pf.Month));
            visitLimit = Math.Max(1, weeklyDays) * 4 * months;
            remaining = Math.Max(0, visitLimit.Value - visited);
            remainingLabel = remaining.Value.ToString();
            packageEnded = remaining.Value <= 0;
            hasCarryover = periodExpired && remaining.Value > 0;
        }
        else
        {
            remaining = remainingPlanned;
            remainingLabel = remainingPlanned.ToString();
        }

        return new
        {
            visited,
            remaining,
            remainingLabel,
            visitLimit,
            weeklyDays,
            isUnlimited,
            hasActiveSubscription = activeSchedules.Count > 0,
            periodFromLocal = periodFrom?.ToString("yyyy-MM-dd"),
            periodToLocal = periodTo?.ToString("yyyy-MM-dd"),
            periodExpired,
            packageEnded,
            hasCarryover
        };
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
