using EShooting.Application.Equipment.Commands;
using EShooting.Application.Equipment.Queries;
using EShooting.Web.Contracts.Equipment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("admin/equipment")]
public sealed class AdminEquipmentController(IMediator mediator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new GetEquipmentItemsQuery(ActiveOnly: false), cancellationToken);
        return View("~/Views/Admin/Equipment/Index.cshtml", items);
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
            Quantity = item.Quantity,
            Price = item.Price,
            IsActive = item.IsActive
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

        model.IsActive = Request.Form.ContainsKey("IsActive");
        var price = model.Price is > 0 ? model.Price : null;

        try
        {
            await mediator.Send(new UpsertEquipmentItemCommand(
                model.Id,
                model.Name,
                model.Category,
                model.Quantity,
                price,
                model.IsActive), cancellationToken);

            TempData["EquipmentNotice"] = model.Id is null
                ? (model.IsActive ? "Avadanlıq yaradıldı və aktiv edildi." : "Avadanlıq yaradıldı.")
                : (model.IsActive ? "Avadanlıq yeniləndi və aktivdir." : "Avadanlıq yeniləndi.");
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("~/Views/Admin/Equipment/Form.cshtml", model);
        }
    }
}
