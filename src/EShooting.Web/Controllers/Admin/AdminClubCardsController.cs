using EShooting.Application.Athletes;
using EShooting.Application.Athletes.Commands;
using EShooting.Application.Athletes.Queries;
using EShooting.Domain.Enums;
using EShooting.Web.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Authorize(Policy = AdminAuthDefaults.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("admin/club-cards")]
public sealed class AdminClubCardsController(IMediator mediator) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        ClubCardType? filterType,
        string? status,
        ClubCardType? lookupType,
        string? lookupNumbers,
        CancellationToken cancellationToken)
    {
        var resolvedStatus = string.IsNullOrWhiteSpace(status) ? "held" : status;
        var summary = await mediator.Send(new GetClubCardStockSummaryQuery(), cancellationToken);
        var catalog = await mediator.Send(new GetClubCardCatalogQuery(filterType, resolvedStatus), cancellationToken);
        ViewBag.Catalog = catalog;
        ViewBag.FilterType = filterType;
        ViewBag.Status = resolvedStatus;

        var numbers = ClubCardNumberRules.ParseMany(lookupNumbers);
        if (lookupType is ClubCardType type && numbers.Count > 0)
        {
            var found = await mediator.Send(new GetClubCardLookupsQuery(type, numbers), cancellationToken);
            ViewBag.Lookups = found;
            ViewBag.LookupType = type;
            ViewBag.LookupNumbers = string.Join(", ", numbers);
        }

        return View("~/Views/Admin/ClubCards/Index.cshtml", summary);
    }

    [HttpGet("export.xlsx")]
    public async Task<IActionResult> ExportSummary(CancellationToken cancellationToken)
    {
        var summary = await mediator.Send(new GetClubCardStockSummaryQuery(), cancellationToken);
        var bytes = AdminClubCardsExcelExporter.ExportSummary(summary);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Kartlar-icmal.xlsx");
    }

    [HttpGet("catalog/export.xlsx")]
    public async Task<IActionResult> ExportCatalog(
        ClubCardType? filterType,
        string? status,
        CancellationToken cancellationToken)
    {
        var resolvedStatus = string.IsNullOrWhiteSpace(status) ? "held" : status;
        var catalog = await mediator.Send(new GetClubCardCatalogQuery(filterType, resolvedStatus), cancellationToken);
        var title = CatalogStatusTitle(resolvedStatus);
        var bytes = AdminClubCardsExcelExporter.ExportCatalog(catalog, title);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Kart-melumatlari.xlsx");
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        string? mode,
        ClubCardType? cardType,
        int? fromNumber,
        int? toNumber,
        string? cardNumber,
        CancellationToken cancellationToken)
    {
        if (cardType is not ClubCardType type)
        {
            TempData["ClubCardError"] = "Kart növü seçin.";
            return RedirectToIndex();
        }

        var isOne = string.Equals(mode, "one", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (isOne)
            {
                var normalized = ClubCardNumberRules.Normalize(cardNumber);
                if (string.IsNullOrWhiteSpace(normalized) || !int.TryParse(normalized, out var n) || n < 1)
                {
                    TempData["ClubCardError"] = "Kart nömrəsi daxil edin.";
                    return RedirectToIndex();
                }

                var result = await mediator.Send(new AddClubCardStockRangeCommand(type, n, n), cancellationToken);
                var label = ClubCardTypeLabels.FormatCard(type, ClubCardNumberRules.Format(n));
                TempData["ClubCardNotice"] = result.Added > 0
                    ? $"Əlavə olundu: {label}."
                    : $"Kart «{label}» artıq var (keçildi).";
            }
            else
            {
                if (fromNumber is not int from || toNumber is not int to)
                {
                    TempData["ClubCardError"] = "Başlanğıc və bitmə nömrəsini daxil edin.";
                    return RedirectToIndex();
                }

                var result = await mediator.Send(new AddClubCardStockRangeCommand(type, from, to), cancellationToken);
                var typeLabel = ClubCardTypeLabels.Get(type);
                TempData["ClubCardNotice"] =
                    $"{typeLabel}: {ClubCardNumberRules.Format(from)}–{ClubCardNumberRules.Format(to)} → {result.Added} əlavə olundu"
                    + (result.Skipped > 0 ? $", {result.Skipped} artıq var idi (keçildi)." : ".");
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["ClubCardError"] = ex.Message;
        }

        return RedirectToIndex();
    }

    [HttpPost("find")]
    [ValidateAntiForgeryToken]
    public IActionResult Find(ClubCardType? cardType, string? cardNumbers)
    {
        if (cardType is not ClubCardType type)
        {
            TempData["ClubCardError"] = "Kart növü seçin.";
            return RedirectToIndex();
        }

        var numbers = ClubCardNumberRules.ParseMany(cardNumbers);
        if (numbers.Count == 0)
        {
            TempData["ClubCardError"] = "Kart nömrəsi daxil edin.";
            return RedirectToIndex();
        }

        return RedirectToIndex(type, string.Join(", ", numbers));
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        ClubCardType? cardType,
        string? cardNumbers,
        bool confirmHeld,
        CancellationToken cancellationToken)
    {
        if (cardType is not ClubCardType type)
        {
            TempData["ClubCardError"] = "Kart növü seçin.";
            return RedirectToIndex();
        }

        var numbers = ClubCardNumberRules.ParseMany(cardNumbers);
        if (numbers.Count == 0)
        {
            TempData["ClubCardError"] = "Kart nömrəsi daxil edin.";
            return RedirectToIndex();
        }

        var lookups = await mediator.Send(new GetClubCardLookupsQuery(type, numbers), cancellationToken);
        var held = lookups.Where(x => x.IsHeld).ToList();
        if (held.Count > 0 && !confirmHeld)
        {
            return RedirectToIndex(type, string.Join(", ", numbers));
        }

        var deleted = 0;
        var skipped = 0;
        var missing = 0;
        foreach (var item in lookups)
        {
            if (item.Missing)
            {
                missing++;
                continue;
            }

            try
            {
                await mediator.Send(
                    new DeleteClubCardStockCommand(type, item.CardNumber, ReturnIfHeld: confirmHeld),
                    cancellationToken);
                deleted++;
            }
            catch (ClubCardHeldException)
            {
                skipped++;
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
        }

        TempData["ClubCardNotice"] =
            $"{deleted} kart silindi"
            + (skipped > 0 ? $", {skipped} keçildi" : "")
            + (missing > 0 ? $", {missing} stokda yoxdur" : "")
            + ".";
        return RedirectToIndex(type, string.Join(", ", numbers));
    }

    [HttpPost("restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(
        ClubCardType? cardType,
        string? cardNumbers,
        CancellationToken cancellationToken)
    {
        if (cardType is not ClubCardType type)
        {
            TempData["ClubCardError"] = "Kart növü seçin.";
            return RedirectToIndex();
        }

        var numbers = ClubCardNumberRules.ParseMany(cardNumbers);
        if (numbers.Count == 0)
        {
            TempData["ClubCardError"] = "Kart nömrəsi daxil edin.";
            return RedirectToIndex();
        }

        var restored = 0;
        var skipped = 0;
        foreach (var number in numbers)
        {
            try
            {
                await mediator.Send(new RestoreClubCardStockCommand(type, number), cancellationToken);
                restored++;
            }
            catch (InvalidOperationException)
            {
                skipped++;
            }
        }

        TempData["ClubCardNotice"] =
            $"{restored} kart bərpa olundu"
            + (skipped > 0 ? $", {skipped} keçildi" : "")
            + ".";
        return RedirectToIndex(type, string.Join(", ", numbers));
    }

    private RedirectToActionResult RedirectToIndex(ClubCardType? lookupType = null, string? lookupNumbers = null) =>
        RedirectToAction(nameof(Index), new
        {
            filterType = (ClubCardType?)null,
            status = "held",
            lookupType,
            lookupNumbers
        });

    private static string CatalogStatusTitle(string status) => status.Trim().ToLowerInvariant() switch
    {
        "held" => "Müştəridə olan",
        "free" => "Müştəridə olmayan",
        "deleted" => "Mövcud deyil",
        "all" => "Hamısı",
        _ => "Kart məlumatları"
    };
}
