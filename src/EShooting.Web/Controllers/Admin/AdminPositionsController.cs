using EShooting.Application.StaffPositions.Commands;
using EShooting.Application.StaffPositions.Queries;
using EShooting.Web.Contracts.StaffPositions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("admin/positions")]
public sealed class AdminPositionsController(IMediator mediator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new GetStaffPositionsQuery(ActiveOnly: false), cancellationToken);
        return View("~/Views/Admin/Positions/Index.cshtml", items);
    }

    [HttpGet("new")]
    public IActionResult Create() => View("~/Views/Admin/Positions/Form.cshtml", new StaffPositionFormModel());

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffPositionFormModel model, CancellationToken cancellationToken)
        => await SaveAsync(model, cancellationToken);

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetStaffPositionByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        return View("~/Views/Admin/Positions/Form.cshtml", new StaffPositionFormModel
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            IsActive = item.IsActive
        });
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, StaffPositionFormModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        return await SaveAsync(model, cancellationToken);
    }

    [HttpPost("{id:guid}/toggle-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetStaffPositionByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        await mediator.Send(new SetStaffPositionActiveCommand(id, !item.IsActive), cancellationToken);
        TempData["PositionNotice"] = item.IsActive
            ? "Vəzifə deaktiv edildi."
            : "Vəzifə yenidən aktiv edildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetStaffPositionByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        await mediator.Send(new SetStaffPositionDeletedCommand(id, true), cancellationToken);
        TempData["PositionNotice"] = "Silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(StaffPositionFormModel model, CancellationToken cancellationToken)
    {
        model.IsActive = Request.Form.ContainsKey("IsActive");

        try
        {
            await mediator.Send(new UpsertStaffPositionCommand(
                model.Id, model.Name, model.Description, model.IsActive), cancellationToken);

            TempData["PositionNotice"] = model.Id is null ? "Vəzifə yaradıldı." : "Vəzifə yeniləndi.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("~/Views/Admin/Positions/Form.cshtml", model);
        }
    }
}
