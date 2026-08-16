using EShooting.Application.Athletes.Commands;
using EShooting.Application.Athletes.Queries;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using EShooting.Web.Auth;
using EShooting.Web.Contracts.Athletes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

public sealed class CustomerListFilter
{
    public string? Search { get; set; }
    public string? Vip { get; set; }
    public string? PackageType { get; set; }
    public string? CustomerType { get; set; }
    public string? SessionRental { get; set; }
    public string? Active { get; set; }
    public int? Category { get; set; }
    public DateTime? RegisteredFrom { get; set; }
    public DateTime? RegisteredTo { get; set; }
    public bool IncludeInactive { get; set; }
}

public sealed class UpdateCustomerPackagePaymentRequest
{
    public decimal PriceDue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }
    public bool IsComplimentary { get; set; }
}

[Authorize(Policy = AdminAuthDefaults.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("admin/customers")]
public sealed class AdminCustomersController(IMediator mediator, ITrainingCenterRepository repository) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Müştərilər";
        return View("~/Views/Admin/Customers/Index.cshtml");
    }

    [HttpGet("data")]
    public async Task<IActionResult> Data([FromQuery] CustomerListFilter filter, CancellationToken cancellationToken)
    {
        NormalizeCustomerListFilter(filter);
        CustomerCategory? cat = filter.Category is >= 0 and <= 2 ? (CustomerCategory)filter.Category.Value : null;
        var result = await mediator.Send(
            new GetCustomersListQuery(
                filter.Search,
                filter.Vip,
                filter.PackageType,
                filter.CustomerType,
                filter.SessionRental,
                filter.Active,
                cat,
                filter.RegisteredFrom,
                filter.RegisteredTo,
                filter.IncludeInactive),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("export.xlsx")]
    public async Task<IActionResult> Export([FromQuery] CustomerListFilter filter, CancellationToken cancellationToken)
    {
        NormalizeCustomerListFilter(filter);
        CustomerCategory? cat = filter.Category is >= 0 and <= 2 ? (CustomerCategory)filter.Category.Value : null;
        var result = await mediator.Send(
            new GetCustomersListQuery(
                filter.Search,
                filter.Vip,
                filter.PackageType,
                filter.CustomerType,
                filter.SessionRental,
                filter.Active,
                cat,
                filter.RegisteredFrom,
                filter.RegisteredTo,
                filter.IncludeInactive),
            cancellationToken);

        var bytes = AdminCustomersExcelExporter.Export(result.Items);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"musteriler-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("{id:guid}/package-payments")]
    public async Task<IActionResult> PackagePayments(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var items = await mediator.Send(new GetCustomerPackagePaymentsQuery(id), cancellationToken);
            return Ok(new { items });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("package-payments/{recordId:guid}")]
    public async Task<IActionResult> UpdatePackagePayment(
        Guid recordId,
        [FromBody] UpdateCustomerPackagePaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(
                new UpdateCustomerPackageBillingCommand(
                    recordId,
                    request.PriceDue,
                    request.DiscountAmount,
                    request.AmountPaidCash,
                    request.AmountPaidCard,
                    request.IsComplimentary),
                cancellationToken);
            return Ok(new { message = "Ödəniş qeydi yeniləndi." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("package-payments/{recordId:guid}")]
    public async Task<IActionResult> DeletePackagePayment(Guid recordId, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new DeleteCustomerPackageRecordCommand(recordId), cancellationToken);
            return Ok(new { message = "Ödəniş qeydi silindi." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/change-package/preview")]
    public async Task<IActionResult> PreviewChangePackage(
        Guid id,
        [FromQuery] Guid newServicePackageId,
        [FromQuery] decimal discountAmount,
        [FromQuery] bool justRenew,
        CancellationToken cancellationToken)
    {
        try
        {
            var preview = await ChangeCustomerPackageCommandHandler.BuildPreviewAsync(
                repository,
                id,
                newServicePackageId,
                discountAmount,
                cancellationToken,
                justRenew);
            return Ok(new
            {
                athleteId = preview.AthleteId,
                athleteName = preview.AthleteName,
                oldPackageName = preview.OldPackageName,
                oldAmountPaid = preview.OldAmountPaid,
                newServicePackageId = preview.NewServicePackageId,
                newPackageName = preview.NewPackageName,
                newListPrice = preview.NewListPrice,
                discountAmount = preview.DiscountAmount,
                newPayable = preview.NewPayable,
                appliedCredit = preview.AppliedCredit,
                additionalDue = preview.AdditionalDue,
                refundDue = preview.RefundDue,
                differenceKind = preview.DifferenceKind,
                isFixedWeekly = preview.IsFixedWeekly,
                isFlexibleMonthly = preview.IsFlexibleMonthly,
                defaultWeeklyDaysCsv = preview.DefaultWeeklyDaysCsv,
                weeklyDaysCount = preview.WeeklyDaysCount,
                visitQuota = preview.VisitQuota,
                sessionDurationMinutes = preview.SessionDurationMinutes,
                validityDays = preview.ValidityDays,
                requiresNewPayment = preview.RequiresNewPayment,
                lifecycleHint = preview.LifecycleHint
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/change-package")]
    public async Task<IActionResult> ChangePackage(
        Guid id,
        [FromBody] ChangeCustomerPackageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            TimeSpan? weeklyStart = null;
            if (!string.IsNullOrWhiteSpace(request.WeeklyStartTimeLocal)
                && TimeSpan.TryParse(request.WeeklyStartTimeLocal.Trim(), out var parsedStart))
            {
                weeklyStart = parsedStart;
            }

            var result = await mediator.Send(
                new ChangeCustomerPackageCommand(
                    id,
                    request.NewServicePackageId,
                    request.PeriodStartLocal,
                    request.PeriodEndLocal,
                    request.PeriodMonths,
                    request.DiscountAmount,
                    request.AmountPaidCash,
                    request.AmountPaidCard,
                    request.IsComplimentary,
                    request.ConfirmDifference,
                    request.SkipPayment,
                    request.WeeklyDaysOfWeek,
                    weeklyStart,
                    CreatedByStaffId: null,
                    CanApplyDiscount: true,
                    CanGrantComplimentary: true),
                cancellationToken);
            return Ok(new
            {
                newPackageRecordId = result.NewPackageRecordId,
                refundRecordId = result.RefundRecordId,
                message = result.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(
                new SetAthleteActiveCommand(
                    id,
                    IsActive: false,
                    DeletedByStaffId: null,
                    DeletedByAdminUserName: User.Identity?.Name),
                cancellationToken);
            return Ok(new { message = "Müştəri silindi." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new SetAthleteActiveCommand(id, IsActive: true), cancellationToken);
            return Ok(new { message = "Müştəri bərpa edildi." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/hard-delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new HardDeleteAthleteCommand(id), cancellationToken);
            return Ok(new { message = "Müştəri birdəfəlik silindi." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public IActionResult Detail([FromRoute] Guid id) =>
        RedirectToAction(nameof(Index));

    private static void NormalizeCustomerListFilter(CustomerListFilter filter)
    {
        var activeKey = (filter.Active ?? "").Trim().ToLowerInvariant();
        if (activeKey is "inactive" or "deleted" or "silinmis")
        {
            filter.IncludeInactive = true;
        }
        else if (activeKey is "" or "all" or "hamisi")
        {
            filter.IncludeInactive = true;
        }
        else
        {
            filter.IncludeInactive = false;
        }

        // Tarix boşdursa məcburi «bu gün» qoymırıq — bütün müştərilər gəlir.
        // Binding Kind (UTC/Local) qarışıqlığını aradan qaldırmaq üçün yalnız tarix hissəsi.
        filter.RegisteredFrom = AsFilterDate(filter.RegisteredFrom);
        filter.RegisteredTo = AsFilterDate(filter.RegisteredTo);
    }

    private static DateTime? AsFilterDate(DateTime? value)
    {
        if (value is not DateTime dt)
        {
            return null;
        }

        return DateTime.SpecifyKind(dt.Date, DateTimeKind.Unspecified);
    }
}
