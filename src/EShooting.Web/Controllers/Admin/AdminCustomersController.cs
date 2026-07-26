using EShooting.Application.Athletes.Commands;
using EShooting.Application.Athletes.Queries;
using EShooting.Domain.Enums;
using EShooting.Web.Auth;
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

[Authorize(Policy = AdminAuthDefaults.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("admin/customers")]
public sealed class AdminCustomersController(IMediator mediator) : Controller
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
