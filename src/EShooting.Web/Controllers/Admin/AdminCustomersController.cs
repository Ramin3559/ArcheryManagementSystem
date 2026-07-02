using EShooting.Application.Athletes.Queries;
using EShooting.Application.Common;
using EShooting.Application.Common.Models;
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
        EnsureCustomerListDates(filter);
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
        EnsureCustomerListDates(filter);
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

    [HttpGet("{id:guid}")]
    public IActionResult Detail([FromRoute] Guid id) =>
        RedirectToAction(nameof(Index));

    private static void EnsureCustomerListDates(CustomerListFilter filter)
    {
        if (filter.RegisteredFrom is null && filter.RegisteredTo is null)
        {
            filter.RegisteredFrom = AzerbaijanTime.TodayLocal;
            filter.RegisteredTo = AzerbaijanTime.TodayLocal;
        }
    }
}
