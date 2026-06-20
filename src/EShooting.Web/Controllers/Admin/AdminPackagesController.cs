using EShooting.Application.Packages;
using EShooting.Application.Packages.Commands;
using EShooting.Application.Packages.Queries;
using EShooting.Domain.Enums;
using EShooting.Web.Contracts.Packages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Roles = "Admin")]
[Route("admin/packages")]
public sealed class AdminPackagesController(IMediator mediator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var packages = await mediator.Send(new GetServicePackagesQuery(ActiveOnly: false), cancellationToken);
        return View("~/Views/Admin/Packages/Index.cshtml", packages);
    }

    [HttpGet("new")]
    public IActionResult Create()
    {
        return View("~/Views/Admin/Packages/Form.cshtml", new ServicePackageFormModel());
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServicePackageFormModel model, CancellationToken cancellationToken)
    {
        return await SaveAsync(model, cancellationToken);
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetServicePackageByIdQuery(id), cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return View("~/Views/Admin/Packages/Form.cshtml", new ServicePackageFormModel
        {
            Id = item.Id,
            Name = item.Name,
            BillingType = item.BillingType,
            Price = item.Price,
            SessionDurationMinutes = item.SessionDurationMinutes,
            IsActive = item.IsActive
        });
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ServicePackageFormModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        return await SaveAsync(model, cancellationToken);
    }

    [HttpPost("{id:guid}/toggle-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetServicePackageByIdQuery(id), cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        try
        {
            await mediator.Send(new SetServicePackageActiveCommand(id, !item.IsActive), cancellationToken);
            TempData["PackageNotice"] = item.IsActive
                ? "Paket deaktiv edildi — resepsiya siyahısında görünməyəcək."
                : "Paket yenidən aktiv edildi.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["PackageError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await mediator.Send(new GetServicePackageByIdQuery(id), cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        await mediator.Send(new SetServicePackageDeletedCommand(id, true), cancellationToken);
        TempData["PackageNotice"] = "Silindi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SaveAsync(ServicePackageFormModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Paket adı mütləqdir.");
        }

        model.IsActive = Request.Form.ContainsKey("IsActive");

        try
        {
            var scope = PackageScope.Archery;
            var scheduling = model.BillingType == PackageBillingType.OneTime
                ? PackageSchedulingMode.None
                : PackageSchedulingMode.FixedWeekly;
            int? validity = model.BillingType switch
            {
                PackageBillingType.Monthly => 30,
                PackageBillingType.Yearly => 365,
                _ => null
            };

            var id = await mediator.Send(new UpsertServicePackageCommand(
                model.Id,
                model.Name,
                model.BillingType,
                scope,
                scheduling,
                model.Price,
                model.SessionDurationMinutes,
                PeriodMinutesQuota: null,
                WeeklyDaysCsv: null,
                validity,
                UnlimitedGym: false,
                model.IsActive), cancellationToken);

            TempData["PackageNotice"] = model.Id is null
                ? (model.IsActive ? "Paket yaradıldı və aktiv edildi." : "Paket yaradıldı.")
                : (model.IsActive ? "Paket yeniləndi və aktivdir." : "Paket yeniləndi.");
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("~/Views/Admin/Packages/Form.cshtml", model);
        }
    }
}
