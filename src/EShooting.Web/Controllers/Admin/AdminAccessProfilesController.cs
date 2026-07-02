using EShooting.Application.AccessProfiles.Commands;
using EShooting.Application.AccessProfiles.Queries;
using EShooting.Application.Common.Models;
using EShooting.Web.Auth;
using EShooting.Web.Contracts.AccessProfiles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Policy = AdminAuthDefaults.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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

        return View("~/Views/Admin/AccessProfiles/Form.cshtml", MapToForm(item));
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
        model.IsActive = true;

        try
        {
            await mediator.Send(new UpsertAccessProfileCommand(
                model.Id,
                model.Name,
                model.Description,
                model.CanRegisterCustomers,
                model.CanViewCustomerDetails,
                model.CanEditCustomerDetails,
                model.CanManageSubscriptions,
                model.CanManageSubscriptions,
                model.CanApplyDiscount,
                model.CanGrantComplimentarySession,
                model.CanManageSessions,
                model.CanManageEquipment,
                model.CanSellEquipment,
                model.CanReturnEquipment,
                model.CanAccessPlanset,
                model.CanIssueEquipmentRental,
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
        model.CanViewCustomerDetails = Request.Form.ContainsKey("CanViewCustomerDetails");
        model.CanEditCustomerDetails = Request.Form.ContainsKey("CanEditCustomerDetails");
        model.CanManageSubscriptions = Request.Form.ContainsKey("CanManageSubscriptions");
        model.CanApplyDiscount = Request.Form.ContainsKey("CanApplyDiscount");
        model.CanGrantComplimentarySession = Request.Form.ContainsKey("CanGrantComplimentarySession");
        model.CanManageSessions = Request.Form.ContainsKey("CanManageSessions");
        model.CanManageEquipment = Request.Form.ContainsKey("CanManageEquipment");
        model.CanSellEquipment = Request.Form.ContainsKey("CanSellEquipment");
        model.CanReturnEquipment = Request.Form.ContainsKey("CanReturnEquipment");
        model.CanAccessPlanset = Request.Form.ContainsKey("CanAccessPlanset");
        model.CanIssueEquipmentRental = Request.Form.ContainsKey("CanIssueEquipmentRental");
        model.CanViewHistory = Request.Form.ContainsKey("CanViewHistory");
    }

    private static AccessProfileFormModel MapToForm(AccessProfileItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        CanRegisterCustomers = item.CanRegisterCustomers,
        CanViewCustomerDetails = item.CanViewCustomerDetails,
        CanEditCustomerDetails = item.CanEditCustomerDetails,
        CanManageSubscriptions = item.CanManageSubscriptions,
        CanApplyDiscount = item.CanApplyDiscount,
        CanGrantComplimentarySession = item.CanGrantComplimentarySession,
        CanManageSessions = item.CanManageSessions,
        CanManageEquipment = item.CanManageEquipment,
        CanSellEquipment = item.CanSellEquipment,
        CanReturnEquipment = item.CanReturnEquipment,
        CanAccessPlanset = item.CanAccessPlanset,
        CanIssueEquipmentRental = item.CanIssueEquipmentRental,
        CanViewHistory = item.CanViewHistory,
        IsActive = item.IsActive
    };
}
