using EShooting.Application.StaffMembers.Queries;
using EShooting.Web.Auth;
using EShooting.Web.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

/// <summary>
/// Resepsiya PIN girişi və çıxışı.
/// </summary>
[Route("resepsiya")]
public sealed class ReceptionController(IMediator mediator) : Controller
{
  [HttpGet("giris")]
  [AllowAnonymous]
  public async Task<IActionResult> Giris()
  {
    if (User.IsReceptionStaff())
    {
      return Redirect("/qeydiyyat");
    }

    if (User.Identity?.IsAuthenticated == true)
    {
      await HttpContext.SignOutReceptionAsync();
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

    await ReceptionStaffSignIn.SignInAsync(HttpContext, session);
    return Redirect("/qeydiyyat");
  }

  [HttpPost("cixis")]
  [Authorize(Policy = "ReceptionPanel")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> Cixis()
  {
    await HttpContext.SignOutReceptionAsync();
    return RedirectToAction(nameof(Giris));
  }
}
