using System.Globalization;
using EShooting.Application.Analytics.Queries;
using EShooting.Application.Common.Models;
using MediatR;
using EShooting.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Policy = AdminAuthDefaults.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
        [FromQuery] string? section,
        CancellationToken cancellationToken)
    {
        var result = await LoadAsync(mode, fromDate, toDate, month, cancellationToken);
        var sectionKey = (section ?? "all").Trim().ToLowerInvariant();
        var bytes = AdminAnalyticsExcelExporter.Export(result, sectionKey);
        var sectionSuffix = sectionKey == "all" ? "Hamisi" : sectionKey;
        var name = $"EShooting-Hesabat-{sectionSuffix}-{result.FromLocal}-{result.ToLocal}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [HttpPost("export-grid.xlsx")]
    public IActionResult ExportGrid([FromBody] AnalyticsGridExportRequest request)
    {
        if (request.Headers is null || request.Headers.Count == 0)
        {
            return BadRequest(new { error = "Cədvəl başlıqları boşdur." });
        }

        var rows = request.Rows ?? [];
        var bytes = AdminAnalyticsExcelExporter.ExportGrid(
            request.SheetName ?? "Hesabat",
            request.Subtitle,
            request.Headers,
            rows);

        var safeSheet = (request.SheetName ?? "Hesabat").Replace(" ", "-");
        var name = $"EShooting-{safeSheet}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
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
        equipmentRentalIssuedCount = result.EquipmentRentalIssuedCount,
        equipmentRentalReturnedCount = result.EquipmentRentalReturnedCount,
        equipmentRentalOutstandingCount = result.EquipmentRentalOutstandingCount,
        equipmentSaleRevenue = result.EquipmentSaleRevenue,
        totalLaneHours = result.TotalLaneHours,
        busiestLaneNumber = result.BusiestLaneNumber,
        packageRecordCount = result.PackageRecordCount,
        complimentaryCount = result.ComplimentaryCount,
        packagePriceDue = result.PackagePriceDue,
        packagePaidCash = result.PackagePaidCash,
        packagePaidCard = result.PackagePaidCard,
        packagePaidTotal = result.PackagePaidTotal,
        packageRemaining = result.PackageRemaining,
        standaloneEquipmentSaleCount = result.StandaloneEquipmentSaleCount,
        standaloneEquipmentSaleDue = result.StandaloneEquipmentSaleDue,
        standaloneEquipmentPaidCash = result.StandaloneEquipmentPaidCash,
        standaloneEquipmentPaidCard = result.StandaloneEquipmentPaidCard,
        standaloneEquipmentPaidTotal = result.StandaloneEquipmentPaidTotal,
        standaloneEquipmentRemaining = result.StandaloneEquipmentRemaining,
        totalPriceDue = result.TotalPriceDue,
        totalPaidCash = result.TotalPaidCash,
        totalPaidCard = result.TotalPaidCard,
        totalPaid = result.TotalPaid,
        totalRemaining = result.TotalRemaining,
        dailyBreakdown = result.DailyBreakdown.Select(MapDailyRow),
        dailyTotals = MapDailyTotals(result.DailyTotals),
        laneActivity = result.LaneActivity.Select(x => new
        {
            laneNumber = x.LaneNumber,
            sessionCount = x.SessionCount,
            totalHours = x.TotalHours
        }),
        equipmentSaleDetails = result.EquipmentSaleDetails.Select(x => new
        {
            dateLocal = x.DateLocal,
            timeLocal = x.TimeLocal,
            equipmentName = x.EquipmentName,
            totalQuantity = x.TotalQuantity,
            inHallQuantity = x.InHallQuantity,
            forSaleQuantity = x.ForSaleQuantity,
            soldQuantity = x.SoldQuantity,
            unitPrice = x.UnitPrice,
            lineTotal = x.LineTotal,
            paidCash = x.PaidCash,
            paidCard = x.PaidCard,
            discountAmount = x.DiscountAmount,
            customerName = x.CustomerName,
            soldByStaffName = x.SoldByStaffName,
            saleSource = x.SaleSource
        }),
        customerVisitDetails = result.CustomerVisitDetails.Select(x => new
        {
            dateLocal = x.DateLocal,
            customerName = x.CustomerName,
            phone = x.Phone,
            receptionStaffName = x.ReceptionStaffName,
            supervisorStaffName = x.SupervisorStaffName,
            packageName = x.PackageName,
            recordedAtLocal = x.RecordedAtLocal,
            laneNumber = x.LaneNumber,
            startTimeLocal = x.StartTimeLocal,
            endTimeLocal = x.EndTimeLocal,
            durationHours = x.DurationHours,
            durationLabel = x.DurationLabel,
            priceDue = x.PriceDue,
            amountPaidCash = x.AmountPaidCash,
            amountPaidCard = x.AmountPaidCard,
            amountPaid = x.AmountPaid,
            discountAmount = x.DiscountAmount,
            isComplimentary = x.IsComplimentary
        })
    };

    private static object MapDailyRow(DailyOperationsRow x) => new
    {
        dateLocal = x.DateLocal,
        uniqueCustomerCount = x.UniqueCustomerCount,
        newCustomerCount = x.NewCustomerCount,
        sessionCount = x.SessionCount,
        subscriptionCreatedCount = x.SubscriptionCreatedCount,
        equipmentSaleCount = x.EquipmentSaleCount,
        equipmentRentalIssuedCount = x.EquipmentRentalIssuedCount,
        equipmentRentalReturnedCount = x.EquipmentRentalReturnedCount,
        equipmentSaleRevenue = x.EquipmentSaleRevenue,
        laneHoursTotal = x.LaneHoursTotal,
        packageRecordCount = x.PackageRecordCount,
        complimentaryCount = x.ComplimentaryCount,
        packagePriceDue = x.PackagePriceDue,
        packagePaidCash = x.PackagePaidCash,
        packagePaidCard = x.PackagePaidCard,
        packagePaidTotal = x.PackagePaidTotal,
        packageRemaining = x.PackageRemaining,
        standaloneEquipmentSaleCount = x.StandaloneEquipmentSaleCount,
        standaloneEquipmentSaleDue = x.StandaloneEquipmentSaleDue,
        standaloneEquipmentPaidCash = x.StandaloneEquipmentPaidCash,
        standaloneEquipmentPaidCard = x.StandaloneEquipmentPaidCard,
        standaloneEquipmentPaidTotal = x.StandaloneEquipmentPaidTotal,
        standaloneEquipmentRemaining = x.StandaloneEquipmentRemaining,
        totalPriceDue = x.TotalPriceDue,
        totalPaidCash = x.TotalPaidCash,
        totalPaidCard = x.TotalPaidCard,
        totalPaid = x.TotalPaid,
        totalRemaining = x.TotalRemaining
    };

    private static object MapDailyTotals(DailyBreakdownTotals x) => new
    {
        sessionCount = x.SessionCount,
        uniqueCustomerCount = x.UniqueCustomerCount,
        newCustomerCount = x.NewCustomerCount,
        subscriptionCreatedCount = x.SubscriptionCreatedCount,
        equipmentSaleCount = x.EquipmentSaleCount,
        equipmentRentalIssuedCount = x.EquipmentRentalIssuedCount,
        equipmentRentalReturnedCount = x.EquipmentRentalReturnedCount,
        equipmentSaleRevenue = x.EquipmentSaleRevenue,
        laneHoursTotal = x.LaneHoursTotal,
        packageRecordCount = x.PackageRecordCount,
        complimentaryCount = x.ComplimentaryCount,
        packagePriceDue = x.PackagePriceDue,
        packagePaidCash = x.PackagePaidCash,
        packagePaidCard = x.PackagePaidCard,
        packagePaidTotal = x.PackagePaidTotal,
        packageRemaining = x.PackageRemaining,
        standaloneEquipmentSaleCount = x.StandaloneEquipmentSaleCount,
        standaloneEquipmentSaleDue = x.StandaloneEquipmentSaleDue,
        standaloneEquipmentPaidCash = x.StandaloneEquipmentPaidCash,
        standaloneEquipmentPaidCard = x.StandaloneEquipmentPaidCard,
        standaloneEquipmentPaidTotal = x.StandaloneEquipmentPaidTotal,
        standaloneEquipmentRemaining = x.StandaloneEquipmentRemaining,
        totalPriceDue = x.TotalPriceDue,
        totalPaidCash = x.TotalPaidCash,
        totalPaidCard = x.TotalPaidCard,
        totalPaid = x.TotalPaid,
        totalRemaining = x.TotalRemaining
    };
}

public sealed record AnalyticsGridExportRequest(
    string? SheetName,
    string? Subtitle,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>>? Rows);
