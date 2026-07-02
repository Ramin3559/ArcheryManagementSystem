using EShooting.Application.Common;
using EShooting.Application.Equipment.Commands;
using EShooting.Application.Equipment.Queries;
using EShooting.Application.StaffMembers.Queries;
using EShooting.Domain.Enums;
using EShooting.Web.Auth;
using EShooting.Web.Contracts.Equipment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Policy = AdminAuthDefaults.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("admin/equipment")]
public sealed class AdminEquipmentController(IMediator mediator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new GetEquipmentItemsQuery(ActiveOnly: false), cancellationToken);
        return View("~/Views/Admin/Equipment/Index.cshtml", items);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var staff = await mediator.Send(new GetStaffMembersQuery(ActiveOnly: false), cancellationToken);
        var equipmentItems = await mediator.Send(new GetEquipmentItemsQuery(ActiveOnly: false), cancellationToken);
        ViewData["StaffMembers"] = staff.OrderBy(x => x.FullName).ToList();
        ViewData["EquipmentItems"] = equipmentItems.OrderBy(x => x.Name).ToList();
        ViewData["Title"] = "Avadanlıq jurnalı";
        return View("~/Views/Admin/Equipment/History.cshtml");
    }

    [HttpGet("history/data")]
    public async Task<IActionResult> HistoryData([FromQuery] EquipmentHistoryFilter filter, CancellationToken cancellationToken)
    {
        ApplyDefaultTodayFilter(filter);
        var result = await LoadHistoryAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("export.xlsx")]
    public async Task<IActionResult> ExportCatalog(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new GetEquipmentItemsQuery(ActiveOnly: false), cancellationToken);
        var bytes = AdminEquipmentCatalogExcelExporter.Export(items);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"avadanliqlar-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("history/export.xlsx")]
    public async Task<IActionResult> ExportHistory([FromQuery] EquipmentHistoryFilter filter, CancellationToken cancellationToken)
    {
        ApplyDefaultTodayFilter(filter);
        var result = await LoadHistoryAsync(filter, cancellationToken);
        var bytes = AdminEquipmentHistoryExcelExporter.Export(result);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"avadanliq-jurnal-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("new")]
    public IActionResult Create()
    {
        return View("~/Views/Admin/Equipment/Form.cshtml", new EquipmentItemFormModel());
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EquipmentItemFormModel model, CancellationToken cancellationToken)
    {
        return await SaveAsync(model, cancellationToken);
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetEquipmentItemByIdQuery(id), cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return View("~/Views/Admin/Equipment/Form.cshtml", new EquipmentItemFormModel
        {
            Id = item.Id,
            Name = item.Name,
            Category = item.Category,
            RentalQuantity = item.RentalQuantity,
            SaleQuantity = item.SaleQuantity,
            DamagedQuantity = item.DamagedQuantity,
            Price = item.Price
        });
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EquipmentItemFormModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        return await SaveAsync(model, cancellationToken);
    }

    [HttpPost("{id:guid}/toggle-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetEquipmentItemByIdQuery(id), cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        try
        {
            await mediator.Send(new SetEquipmentItemActiveCommand(id, !item.IsActive), cancellationToken);
            TempData["EquipmentNotice"] = item.IsActive
                ? "Avadanlıq deaktiv edildi — resepsiya siyahısında görünməyəcək."
                : "Avadanlıq yenidən aktiv edildi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["EquipmentError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetEquipmentItemByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        await mediator.Send(new SetEquipmentItemDeletedCommand(id, true), cancellationToken);
        TempData["EquipmentNotice"] = "Silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(EquipmentItemFormModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Avadanlıq adı mütləqdir.");
        }

        var price = model.Price is > 0 ? model.Price : null;

        try
        {
            await mediator.Send(new UpsertEquipmentItemCommand(
                model.Id,
                model.Name,
                model.Category,
                model.RentalQuantity,
                model.SaleQuantity,
                model.DamagedQuantity,
                price), cancellationToken);

            TempData["EquipmentNotice"] = model.Id is null ? "Avadanlıq yaradıldı." : "Avadanlıq yeniləndi.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("~/Views/Admin/Equipment/Form.cshtml", model);
        }
    }

    private async Task<Application.Common.Models.EquipmentIssueHistoryResult> LoadHistoryAsync(
        EquipmentHistoryFilter filter,
        CancellationToken cancellationToken)
    {
        var from = filter.FromLocal ?? AzerbaijanTime.TodayLocal;
        var to = filter.ToLocal ?? from;
        return await mediator.Send(
            new GetEquipmentIssueHistoryQuery(
                from,
                to,
                filter.EquipmentItemId,
                filter.IssueType,
                filter.IssuedByStaffId),
            cancellationToken);
    }

    private static void ApplyDefaultTodayFilter(EquipmentHistoryFilter filter)
    {
        if (filter.FromLocal is null && filter.ToLocal is null)
        {
            filter.FromLocal = AzerbaijanTime.TodayLocal;
            filter.ToLocal = AzerbaijanTime.TodayLocal;
        }
        else
        {
            filter.FromLocal ??= filter.ToLocal;
            filter.ToLocal ??= filter.FromLocal;
        }
    }
}
