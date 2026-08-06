using EShooting.Application.Packages;
using EShooting.Application.Packages.Commands;
using EShooting.Application.Packages.Queries;
using EShooting.Domain.Enums;
using EShooting.Web.Contracts.Packages;
using EShooting.Web.Auth;
using EShooting.Web.Helpers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Policy = AdminAuthDefaults.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
            BillingType = item.BillingType == PackageBillingType.Gym
                ? PackageBillingType.Monthly
                : item.BillingType == PackageBillingType.Vip
                    ? PackageBillingType.Unlimited
                    : item.BillingType,
            Scope = item.BillingType == PackageBillingType.Gym && item.Scope == PackageScope.Archery
                ? PackageScope.Gym
                : item.Scope,
            Price = item.Price,
            SessionDurationMinutes = item.SessionDurationMinutes,
            WeeklyDaysCount = item.WeeklyDaysCount,
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

        // Formda Aktiv checkbox yoxdur — yeni paket aktiv; redaktədə mövcud status qalır.
        if (model.Id is null || model.Id == Guid.Empty)
        {
            model.IsActive = true;
        }
        else
        {
            var existing = await mediator.Send(new GetServicePackageByIdQuery(model.Id.Value), cancellationToken);
            model.IsActive = existing?.IsActive ?? true;
        }

        if (InvariantDecimalParser.ParseOptional(Request.Form["Price"].ToString()) is { } parsedPrice)
        {
            model.Price = parsedPrice;
        }

        try
        {
            // Köhnə «Zal» billing tipi artıq yoxdur — Aylıq + Yalnız Trenajor kimi saxlanır.
            if (model.BillingType == PackageBillingType.Gym)
            {
                model.BillingType = PackageBillingType.Monthly;
                model.Scope = PackageScope.Gym;
            }

            PackageScope scope = model.Scope;
            PackageSchedulingMode scheduling;
            int sessionDuration;
            int? validity;
            var unlimitedGym = false;

            // Köhnə VIP paket növü artıq yoxdur — Limitsiz kimi saxlanır (VIP müştəri bayrağı ayrıdır).
            if (model.BillingType == PackageBillingType.Vip)
            {
                model.BillingType = PackageBillingType.Unlimited;
            }

            // Müddətsiz = 0; vaxtlı paketdə 1–600 dəq.
            if (model.BillingType != PackageBillingType.Unlimited || model.SessionDurationMinutes > 0)
            {
                if (model.SessionDurationMinutes < 0 || model.SessionDurationMinutes > 600)
                {
                    ModelState.AddModelError(nameof(model.SessionDurationMinutes), "Sessiya müddəti 0 (müddətsiz) və ya 1–600 dəqiqə olmalıdır.");
                    return View("~/Views/Admin/Packages/Form.cshtml", model);
                }
            }

            if (scope is not (PackageScope.Archery or PackageScope.Gym or PackageScope.Full or PackageScope.Vip))
            {
                scope = PackageScope.Archery;
            }

            switch (model.BillingType)
            {
                case PackageBillingType.Unlimited:
                    if (scope == PackageScope.Vip) scope = PackageScope.Archery;
                    scheduling = PackageSchedulingMode.WalkInFlexible;
                    sessionDuration = model.SessionDurationMinutes;
                    validity = null;
                    // Limitsiz oxatma / full — Trenajor hüququ avtomatik.
                    unlimitedGym = scope != PackageScope.Gym;
                    break;
                case PackageBillingType.OneTime:
                    if (scope == PackageScope.Vip) scope = PackageScope.Archery;
                    scheduling = PackageSchedulingMode.None;
                    sessionDuration = model.SessionDurationMinutes;
                    validity = null;
                    unlimitedGym = scope is PackageScope.Full or PackageScope.Gym;
                    break;
                default:
                    // Aylıq / İllik
                    if (scope == PackageScope.Vip) scope = PackageScope.Archery;
                    scheduling = PackageSchedulingMode.FixedWeekly;
                    sessionDuration = model.SessionDurationMinutes;
                    validity = model.BillingType switch
                    {
                        PackageBillingType.Monthly => 30,
                        PackageBillingType.Yearly => 365,
                        _ => null
                    };
                    // Aylıq oxatma və «Hər ikisi» — Trenajor hüququ avtomatik; yalnız Trenajor — yox.
                    unlimitedGym = scope != PackageScope.Gym;
                    break;
            }

            int? weeklyDaysCount = null;
            if (scheduling == PackageSchedulingMode.FixedWeekly && sessionDuration > 0)
            {
                weeklyDaysCount = model.WeeklyDaysCount;
                if (weeklyDaysCount is null or < 1 or > 7)
                {
                    ModelState.AddModelError(nameof(model.WeeklyDaysCount), "Həftədə gün sayı 1–7 arası seçilməlidir.");
                    return View("~/Views/Admin/Packages/Form.cshtml", model);
                }
            }

            var id = await mediator.Send(new UpsertServicePackageCommand(
                model.Id,
                model.Name,
                model.BillingType,
                scope,
                scheduling,
                model.Price,
                sessionDuration,
                PeriodMinutesQuota: null,
                WeeklyDaysCsv: null,
                WeeklyDaysCount: weeklyDaysCount,
                validity,
                unlimitedGym,
                model.IsActive), cancellationToken);

            TempData["PackageNotice"] = model.Id is null
                ? "Paket yaradıldı."
                : "Paket yeniləndi.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("~/Views/Admin/Packages/Form.cshtml", model);
        }
    }
}
