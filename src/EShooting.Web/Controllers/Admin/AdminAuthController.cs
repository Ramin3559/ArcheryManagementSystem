using System.Security.Claims;
using EShooting.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers.Admin;

[Route("admin-auth")]
[AllowAnonymous]
public sealed class AdminAuthController(IAdminCredentialStore adminCredentials) : Controller
{
    [HttpGet("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            return RedirectToLocal(returnUrl);
        }

        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Admin/Login.cshtml");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginPost(string userName, string password, string? returnUrl)
    {
        if (adminCredentials.TryValidate(userName, password))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, userName),
                new(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true
                });

            return RedirectToLocal(returnUrl);
        }

        ModelState.AddModelError(string.Empty, "İstifadəçi adı və ya şifrə yanlışdır.");
        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Admin/Login.cshtml");
    }

    [HttpPost("logout")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/admin");
    }
}
