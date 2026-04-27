using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("admin")]
public sealed class AdminController(ITrainingCenterRepository repository) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);

        var totalRegistrations = sessions.Count;
        var activeSubscribers = athletes.Count(a => a.IsSubscriber);
        var todayLocal = DateTime.Now.Date;
        var todaysSessions = sessions.Count(s => DateTimeAssumedLocal(DateTimeAssumedUtc(s.StartTimeUtc)).Date == todayLocal);

        var popularLaneNumber = sessions
            .GroupBy(s => lanes.FirstOrDefault(l => l.Id == s.LaneId)?.Number ?? 0)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        ViewData["TotalRegistrations"] = totalRegistrations;
        ViewData["ActiveSubscribers"] = activeSubscribers;
        ViewData["TodaysSessions"] = todaysSessions;
        ViewData["PopularLane"] = popularLaneNumber <= 0 ? "—" : $"Zolaq {popularLaneNumber}";
        return View("~/Views/Admin/Dashboard.cshtml");
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] HistoryFilter filter, CancellationToken cancellationToken)
    {
        var rows = await QueryHistoryRows(filter, cancellationToken);
        ViewData["Rows"] = rows;
        ViewData["Filter"] = filter;
        return View("~/Views/Admin/History.cshtml");
    }

    [HttpGet("export.xlsx")]
    public async Task<IActionResult> Export([FromQuery] HistoryFilter filter, CancellationToken cancellationToken)
    {
        var rows = await QueryHistoryRows(filter, cancellationToken);

        var bytes = AdminExcelExporter.Export(rows);
        var name = $"EShooting-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    private async Task<List<HistoryRow>> QueryHistoryRows(HistoryFilter filter, CancellationToken cancellationToken)
    {
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var athleteById = athletes.ToDictionary(a => a.Id, a => a);
        var laneNumberById = lanes.ToDictionary(l => l.Id, l => l.Number);

        Guid? athleteIdFilter = null;
        var phone = NormalizeDigits(filter.Phone);
        var email = NormalizeEmail(filter.Email);
        var idCard = NormalizeText(filter.IdCardNumber);
        if (!string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(idCard))
        {
            var match = athletes.FirstOrDefault(a =>
                (!string.IsNullOrWhiteSpace(phone) && NormalizeDigits(a.PhoneNumber) == phone)
                || (!string.IsNullOrWhiteSpace(email) && NormalizeEmail(a.Email) == email)
                || (!string.IsNullOrWhiteSpace(idCard) && string.Equals(NormalizeText(a.IdCardNumber), idCard, StringComparison.OrdinalIgnoreCase)));
            athleteIdFilter = match?.Id;
        }

        DateTime? fromLocal = filter.FromDate?.Date;
        DateTime? toLocal = filter.ToDate?.Date;

        var rows = sessions
            .OrderByDescending(s => DateTimeAssumedUtc(s.StartTimeUtc))
            .Where(s =>
            {
                if (athleteIdFilter is not null && s.AthleteId != athleteIdFilter) return false;
                if (fromLocal is null && toLocal is null) return true;

                var startLocal = DateTimeAssumedLocal(DateTimeAssumedUtc(s.StartTimeUtc));
                var day = startLocal.Date;
                if (fromLocal is not null && day < fromLocal.Value) return false;
                if (toLocal is not null && day > toLocal.Value) return false;
                return true;
            })
            .Select(s =>
            {
                athleteById.TryGetValue(s.AthleteId, out var a);
                laneNumberById.TryGetValue(s.LaneId, out var laneNo);

                var startLocal = DateTimeAssumedLocal(DateTimeAssumedUtc(s.StartTimeUtc));
                var endLocal = DateTimeAssumedLocal(DateTimeAssumedUtc(s.EndTimeUtc));

                return new HistoryRow
                {
                    DateLocal = startLocal.ToString("yyyy-MM-dd"),
                    AthleteName = a?.FullName ?? "—",
                    Phone = a?.PhoneNumber ?? "—",
                    Category = a?.Category.ToString() ?? CustomerCategory.Amateur.ToString(),
                    LaneNumber = laneNo,
                    StartTimeLocal = startLocal.ToString("HH:mm"),
                    EndTimeLocal = endLocal.ToString("HH:mm")
                };
            })
            .ToList();

        return rows;
    }

    private static DateTime DateTimeAssumedUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime DateTimeAssumedLocal(DateTime utc)
    {
        var u = DateTimeAssumedUtc(utc);
        return u.ToLocalTime();
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
}

public sealed class HistoryFilter
{
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? IdCardNumber { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class HistoryRow
{
    public string DateLocal { get; set; } = "";
    public string AthleteName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Category { get; set; } = "";
    public int LaneNumber { get; set; }
    public string StartTimeLocal { get; set; } = "";
    public string EndTimeLocal { get; set; } = "";
}

