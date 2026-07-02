using EShooting.Application.StaffMembers.Queries;
using EShooting.Web.Auth;
using EShooting.Web.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

/// <summary>
/// Planşet nəzarətçi PIN girişi — resepsiya və admin sessiyalarından ayrı cookie.
/// </summary>
[Route("planset")]
public sealed class PlansetController(IMediator mediator) : Controller
{
    [HttpGet("giris")]
    [AllowAnonymous]
    public async Task<IActionResult> Giris()
    {
        if (await HttpContext.IsPlansetAuthenticatedAsync())
        {
            return Redirect("/planset/zolaqlar");
        }

        return View();
    }

    [HttpPost("giris")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Giris(string pin, CancellationToken cancellationToken)
    {
        var session = await mediator.Send(new GetStaffMemberByPinQuery(pin), cancellationToken);
        if (session is null)
        {
            ModelState.AddModelError(string.Empty, "PIN yanlışdır və ya hesab deaktivdir.");
            return View();
        }

        if (!session.CanAccessPlanset)
        {
            ModelState.AddModelError(string.Empty, "Bu hesab planşetə daxil ola bilməz (planşet icazəsi yoxdur).");
            return View();
        }

        await PlansetStaffSignIn.SignInAsync(HttpContext, session);
        return Redirect("/planset/zolaqlar");
    }

    [HttpPost("cixis")]
    [Authorize(Policy = PlansetAuthDefaults.Policy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cixis()
    {
        await HttpContext.SignOutPlansetAsync();
        return RedirectToAction(nameof(Giris));
    }
}
