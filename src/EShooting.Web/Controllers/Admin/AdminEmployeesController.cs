using EShooting.Application.AccessProfiles.Queries;
using EShooting.Application.StaffMembers.Commands;
using EShooting.Application.StaffMembers.Queries;
using EShooting.Application.StaffPositions.Queries;
using EShooting.Web.Contracts.StaffMembers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("admin/employees")]
public sealed class AdminEmployeesController(IMediator mediator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await mediator.Send(new GetStaffMembersQuery(ActiveOnly: false), cancellationToken);
        return View("~/Views/Admin/Employees/Index.cshtml", items);
    }

    [HttpGet("new")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View("~/Views/Admin/Employees/Form.cshtml", await BuildFormModelAsync(new StaffMemberFormModel(), cancellationToken));
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffMemberFormModel model, CancellationToken cancellationToken)
    {
        return await SaveAsync(model, cancellationToken);
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetStaffMemberByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        var model = await BuildFormModelAsync(new StaffMemberFormModel
        {
            Id = item.Id,
            FirstName = item.FirstName,
            LastName = item.LastName,
            StaffPositionId = item.StaffPositionId,
            AccessProfileId = item.AccessProfileId,
            PhoneNumber = item.PhoneNumber,
            IsActive = item.IsActive
        }, cancellationToken);

        return View("~/Views/Admin/Employees/Form.cshtml", model);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, StaffMemberFormModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        return await SaveAsync(model, cancellationToken);
    }

    [HttpPost("{id:guid}/toggle-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetStaffMemberByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        await mediator.Send(new SetStaffMemberActiveCommand(id, !item.IsActive), cancellationToken);
        TempData["EmployeeNotice"] = item.IsActive
            ? "İşçi deaktiv edildi — resepsiyaya daxil ola bilməyəcək."
            : "İşçi yenidən aktiv edildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetStaffMemberByIdQuery(id), cancellationToken);
        if (item is null) return NotFound();

        await mediator.Send(new SetStaffMemberDeletedCommand(id, true), cancellationToken);
        TempData["EmployeeNotice"] = "Silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(StaffMemberFormModel model, CancellationToken cancellationToken)
    {
        model.IsActive = Request.Form.ContainsKey("IsActive");

        try
        {
            await mediator.Send(new UpsertStaffMemberCommand(
                model.Id,
                model.FirstName,
                model.LastName,
                model.StaffPositionId,
                model.AccessProfileId,
                model.PhoneNumber,
                string.IsNullOrWhiteSpace(model.Pin) ? null : model.Pin,
                model.IsActive), cancellationToken);

            TempData["EmployeeNotice"] = model.Id is null
                ? (model.IsActive ? "İşçi yaradıldı və aktiv edildi." : "İşçi yaradıldı.")
                : (model.IsActive ? "İşçi yeniləndi və aktivdir." : "İşçi yeniləndi.");
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("~/Views/Admin/Employees/Form.cshtml", await BuildFormModelAsync(model, cancellationToken));
        }
    }

    private async Task<StaffMemberFormModel> BuildFormModelAsync(StaffMemberFormModel model, CancellationToken cancellationToken)
    {
        var positions = await mediator.Send(new GetStaffPositionsQuery(ActiveOnly: true), cancellationToken);
        var profiles = await mediator.Send(new GetAccessProfilesQuery(ActiveOnly: true), cancellationToken);

        model.Positions = positions;
        model.Profiles = profiles;

        if (model.StaffPositionId != Guid.Empty && positions.All(x => x.Id != model.StaffPositionId))
        {
            var current = await mediator.Send(new GetStaffPositionByIdQuery(model.StaffPositionId), cancellationToken);
            if (current is not null)
            {
                model.Positions = positions.Concat([current]).ToArray();
            }
        }

        if (model.AccessProfileId != Guid.Empty && profiles.All(x => x.Id != model.AccessProfileId))
        {
            var current = await mediator.Send(new GetAccessProfileByIdQuery(model.AccessProfileId), cancellationToken);
            if (current is not null)
            {
                model.Profiles = profiles.Concat([current]).ToArray();
            }
        }

        return model;
    }
}
