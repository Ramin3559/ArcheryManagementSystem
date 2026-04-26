using EShooting.Application.Common.Interfaces;
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
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);

        // Group subscription schedules by package period (from-to) so we can show:
        // - aralıq (ayın neçəsindən neçəsinə)
        // - həftə günləri (məs: B.e, Ç, C)
        // - hər seansın tarixi + saatı + müddəti
        var packages = schedules
            .Where(x => x.AthleteId == athlete.Id && x.IsEnabled)
            .GroupBy(x => new { From = x.ActiveFromDateLocal.Date, To = x.ActiveToDateLocal.Date, x.IsFullPackage })
            .OrderByDescending(g => g.Max(x => x.CreatedAtUtc))
            .Select(g =>
            {
                var from = g.Key.From;
                var to = g.Key.To;
                var dayLabels = g
                    .Select(x => DayLabelAz(x.DayOfWeek))
                    .Distinct()
                    .ToList();

                var occurrences = new List<object>();
                foreach (var s in g.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTimeLocal))
                {
                    var endTimeLocal = s.StartTimeLocal.Add(TimeSpan.FromMinutes(s.DurationMinutes));
                    for (var day = from; day <= to; day = day.AddDays(1))
                    {
                        if ((int)day.DayOfWeek != s.DayOfWeek) continue;
                        occurrences.Add(new
                        {
                            dateLocal = day.ToString("yyyy-MM-dd"),
                            dayLabel = DayLabelAz(s.DayOfWeek),
                            startTime = $"{s.StartTimeLocal:hh\\:mm}",
                            endTime = $"{endTimeLocal:hh\\:mm}",
                            durationHours = Math.Round(s.DurationMinutes / 60.0, 2)
                        });
                    }
                }

                return new
                {
                    fullName = athlete.FullName,
                    fromLocal = from.ToString("yyyy-MM-dd"),
                    toLocal = to.ToString("yyyy-MM-dd"),
                    days = string.Join(", ", dayLabels),
                    occurrences = occurrences
                        .Cast<dynamic>()
                        .OrderBy(x => (string)x.dateLocal)
                        .ToList(),
                    createdAtLocal = g.Max(x => x.CreatedAtUtc).ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    isFullPackage = g.Key.IsFullPackage
                };
            })
            .ToList();

        // Also keep last sessions (optional, for extra context in UI later)
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
            packages,
            lastSessions
        });
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

    private static string DayLabelAz(int dayOfWeek)
    {
        // .NET: Sunday=0 ... Saturday=6
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

