using System.Globalization;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Web;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("info")]
public sealed class InfoController(ITrainingCenterRepository repository) : ControllerBase
{
    [HttpGet("athlete")]
    public async Task<IActionResult> GetAthleteInfo(
        [FromQuery] string? phone,
        [FromQuery] string? email,
        [FromQuery] string? idCardNumber,
        CancellationToken cancellationToken)
    {
        var phoneN = NormalizeDigits(phone);
        var emailN = NormalizeEmail(email);
        var idN = NormalizeText(idCardNumber);

        if (string.IsNullOrWhiteSpace(phoneN) && string.IsNullOrWhiteSpace(emailN) && string.IsNullOrWhiteSpace(idN))
        {
            return BadRequest(new { error = "Zəhmət olmasa bir məlumat daxil edin." });
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = athletes.FirstOrDefault(a =>
            (!string.IsNullOrWhiteSpace(phoneN) && NormalizeDigits(a.PhoneNumber) == phoneN)
            || (!string.IsNullOrWhiteSpace(emailN) && NormalizeEmail(a.Email) == emailN)
            || (!string.IsNullOrWhiteSpace(idN) && string.Equals(NormalizeText(a.IdCardNumber), idN, StringComparison.OrdinalIgnoreCase)));

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

        var lastSessions = sessions
            .Where(x => x.AthleteId == athlete.Id)
            .OrderByDescending(x => x.StartTimeUtc)
            .Take(20)
            .Select(ses =>
            {
                var startLocal = ses.StartTimeUtc.Kind == DateTimeKind.Utc
                    ? ses.StartTimeUtc.ToLocalTime()
                    : DateTime.SpecifyKind(ses.StartTimeUtc, DateTimeKind.Utc).ToLocalTime();
                var endLocal = ses.EndTimeUtc.Kind == DateTimeKind.Utc
                    ? ses.EndTimeUtc.ToLocalTime()
                    : DateTime.SpecifyKind(ses.EndTimeUtc, DateTimeKind.Utc).ToLocalTime();
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
            category = athlete.Category,
            membershipType = athlete.MembershipType,
            isSubscriber = athlete.IsSubscriber,
            isFullPackage = athlete.IsFullPackage,
            isVip = athlete.IsVip,
            packages,
            weeklySchedules,
            occurrencesFlat,
            lastSessions
        });
    }

    private static List<object> BuildFlatOccurrences(string athleteFullName, List<SubscriptionSchedule> schedules)
    {
        var temp = new List<(string dateKey, string startKey, object row)>();
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
                var row = new
                {
                    scheduleId = s.Id,
                    athleteFullName,
                    dateLocal = dateKey,
                    dayLabel = DayLabelAz(s.DayOfWeek),
                    startTime = startKey,
                    endTime = endKey,
                    durationMinutes = dur,
                    laneNumber = lane,
                    laneLabel,
                    preferredLaneType = (int)s.PreferredLaneType,
                    isFullPackage = s.IsFullPackage
                };
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
                var row = new
                {
                    scheduleId = s.Id,
                    athleteFullName,
                    dateLocal = dateKey,
                    dayLabel = DayLabelAz((int)rescheduledDay.DayOfWeek),
                    startTime = startKey,
                    endTime = endKey,
                    durationMinutes = dur,
                    laneNumber = lane,
                    laneLabel,
                    preferredLaneType = (int)s.PreferredLaneType,
                    isFullPackage = s.IsFullPackage,
                    isRescheduled = true
                };
                temp.Add((dateKey, startKey, row));
                addedDates.Add(dateKey);
            }
        }

        return temp
            .OrderBy(x => x.dateKey, StringComparer.Ordinal)
            .ThenBy(x => x.startKey, StringComparer.Ordinal)
            .Select(x => x.row)
            .Cast<object>()
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
