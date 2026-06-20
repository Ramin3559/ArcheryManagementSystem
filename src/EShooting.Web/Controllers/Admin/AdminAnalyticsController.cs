using System.Globalization;
using EShooting.Application.Analytics.Queries;
using EShooting.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[AllowAnonymous]
[Route("admin/analytics")]
public sealed class AdminAnalyticsController(IMediator mediator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var today = DateTime.Now.Date;
        var result = await mediator.Send(new GetOperationsAnalyticsQuery(today, today, "today"), cancellationToken);
        ViewData["Analytics"] = result;
        return View("~/Views/Admin/Analytics.cshtml");
    }

    [HttpGet("data")]
    public async Task<IActionResult> Data(
        [FromQuery] string? mode,
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        [FromQuery] string? month,
        CancellationToken cancellationToken)
    {
        var result = await LoadAsync(mode, fromDate, toDate, month, cancellationToken);
        return Ok(MapJson(result));
    }

    [HttpGet("export.xlsx")]
    public async Task<IActionResult> Export(
        [FromQuery] string? mode,
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        [FromQuery] string? month,
        CancellationToken cancellationToken)
    {
        var result = await LoadAsync(mode, fromDate, toDate, month, cancellationToken);
        var bytes = AdminAnalyticsExcelExporter.Export(result);
        var name = $"EShooting-Analitika-{result.FromLocal}-{result.ToLocal}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    private async Task<OperationsAnalyticsResult> LoadAsync(
        string? mode,
        string? fromDate,
        string? toDate,
        string? month,
        CancellationToken cancellationToken)
    {
        var (from, to, effectiveMode) = ResolveLocalRange(mode, fromDate, toDate, month);
        return await mediator.Send(new GetOperationsAnalyticsQuery(from, to, effectiveMode), cancellationToken);
    }

    internal static (DateTime From, DateTime To, string Mode) ResolveLocalRange(
        string? mode,
        string? fromDate,
        string? toDate,
        string? month)
    {
        var today = DateTime.Now.Date;
        var effectiveMode = (mode ?? "today").Trim().ToLowerInvariant();

        switch (effectiveMode)
        {
            case "today":
                return (today, today, effectiveMode);
            case "yesterday":
                var yesterday = today.AddDays(-1);
                return (yesterday, yesterday, effectiveMode);
            case "last7":
                return (today.AddDays(-6), today, effectiveMode);
            case "month":
            {
                var anchor = today;
                if (!string.IsNullOrWhiteSpace(month)
                    && DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthAnchor))
                {
                    anchor = monthAnchor;
                }

                var from = new DateTime(anchor.Year, anchor.Month, 1);
                var to = from.AddMonths(1).AddDays(-1);
                return (from, to, effectiveMode);
            }
            case "range":
            default:
                effectiveMode = string.IsNullOrWhiteSpace(fromDate) && string.IsNullOrWhiteSpace(toDate) ? "today" : "range";
                if (effectiveMode == "today")
                {
                    return (today, today, effectiveMode);
                }

                if (!DateTime.TryParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromLocal))
                {
                    fromLocal = today;
                }

                if (!DateTime.TryParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toLocal))
                {
                    toLocal = fromLocal;
                }

                return (fromLocal, toLocal, effectiveMode);
        }
    }

    private static object MapJson(OperationsAnalyticsResult result) => new
    {
        mode = result.Mode,
        fromLocal = result.FromLocal,
        toLocal = result.ToLocal,
        label = result.Label,
        uniqueCustomerCount = result.UniqueCustomerCount,
        newCustomerCount = result.NewCustomerCount,
        sessionCount = result.SessionCount,
        subscriptionCreatedCount = result.SubscriptionCreatedCount,
        equipmentSaleCount = result.EquipmentSaleCount,
        equipmentRentalCount = result.EquipmentRentalCount,
        equipmentSaleRevenue = result.EquipmentSaleRevenue,
        equipmentRentalRevenue = result.EquipmentRentalRevenue,
        totalLaneHours = result.TotalLaneHours,
        busiestLaneNumber = result.BusiestLaneNumber,
        dailyBreakdown = result.DailyBreakdown.Select(x => new
        {
            dateLocal = x.DateLocal,
            uniqueCustomerCount = x.UniqueCustomerCount,
            newCustomerCount = x.NewCustomerCount,
            sessionCount = x.SessionCount,
            subscriptionCreatedCount = x.SubscriptionCreatedCount,
            equipmentSaleCount = x.EquipmentSaleCount,
            equipmentRentalCount = x.EquipmentRentalCount,
            equipmentSaleRevenue = x.EquipmentSaleRevenue,
            equipmentRentalRevenue = x.EquipmentRentalRevenue,
            laneHoursTotal = x.LaneHoursTotal
        }),
        laneActivity = result.LaneActivity.Select(x => new
        {
            laneNumber = x.LaneNumber,
            sessionCount = x.SessionCount,
            totalHours = x.TotalHours
        }),
        equipmentBreakdown = result.EquipmentBreakdown.Select(x => new
        {
            equipmentName = x.EquipmentName,
            saleCount = x.SaleCount,
            rentalCount = x.RentalCount,
            saleRevenue = x.SaleRevenue,
            rentalRevenue = x.RentalRevenue
        })
    };
}
