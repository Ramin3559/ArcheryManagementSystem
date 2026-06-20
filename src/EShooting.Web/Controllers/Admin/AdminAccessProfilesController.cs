using EShooting.Application.AccessProfiles.Commands;
using EShooting.Application.AccessProfiles.Queries;
using EShooting.Web.Contracts.AccessProfiles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("admin/access-profiles")]
public sealed class AdminAccessProfilesController(IMediator mediator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new GetAccessProfilesQuery(ActiveOnly: false), cancellationToken);
        return View("~/Views/Admin/AccessProfiles/Index.cshtml", items);
    }

    [HttpGet("new")]
    public IActionResult Create() => View("~/Views/Admin/AccessProfiles/Form.cshtml", new AccessProfileFormModel());

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccessProfileFormModel model, CancellationToken cancellationToken)
        => await SaveAsync(model, cancellationToken);

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetAccessProfileByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        return View("~/Views/Admin/AccessProfiles/Form.cshtml", new AccessProfileFormModel
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            CanRegisterCustomers = item.CanRegisterCustomers,
            CanManageSubscriptions = item.CanManageSubscriptions,
            CanManageSessions = item.CanManageSessions,
            CanManageEquipment = item.CanManageEquipment,
            CanViewHistory = item.CanViewHistory,
            IsActive = item.IsActive
        });
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AccessProfileFormModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        return await SaveAsync(model, cancellationToken);
    }

    [HttpPost("{id:guid}/toggle-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetAccessProfileByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        await mediator.Send(new SetAccessProfileActiveCommand(id, !item.IsActive), cancellationToken);
        TempData["AccessProfileNotice"] = item.IsActive
            ? "İcazə profili deaktiv edildi — işçilərə təyin olunmayacaq."
            : "İcazə profili yenidən aktiv edildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetAccessProfileByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        await mediator.Send(new SetAccessProfileDeletedCommand(id, true), cancellationToken);
        TempData["AccessProfileNotice"] = "Silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(AccessProfileFormModel model, CancellationToken cancellationToken)
    {
        BindPermissions(model);

        try
        {
            await mediator.Send(new UpsertAccessProfileCommand(
                model.Id,
                model.Name,
                model.Description,
                model.CanRegisterCustomers,
                model.CanManageSubscriptions,
                model.CanManageSessions,
                model.CanManageEquipment,
                model.CanViewHistory,
                model.IsActive), cancellationToken);

            TempData["AccessProfileNotice"] = model.Id is null
                ? "İcazə profili yaradıldı."
                : "İcazə profili yeniləndi.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("~/Views/Admin/AccessProfiles/Form.cshtml", model);
        }
    }

    private void BindPermissions(AccessProfileFormModel model)
    {
        model.CanRegisterCustomers = Request.Form.ContainsKey("CanRegisterCustomers");
        model.CanManageSubscriptions = Request.Form.ContainsKey("CanManageSubscriptions");
        model.CanManageSessions = Request.Form.ContainsKey("CanManageSessions");
        model.CanManageEquipment = Request.Form.ContainsKey("CanManageEquipment");
        model.CanViewHistory = Request.Form.ContainsKey("CanViewHistory");
        model.IsActive = Request.Form.ContainsKey("IsActive");
    }
}
