using System.Security.Claims;
using EShooting.Web.Auth;
using EShooting.Web.Extensions;
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
        if (await HttpContext.IsAdminAuthenticatedAsync())
        {
            return RedirectToLocal(returnUrl);
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
            var identity = new ClaimsIdentity(
                claims,
                AdminAuthDefaults.Scheme,
                ClaimTypes.Name,
                ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAdminAsync(principal);

            return RedirectToLocal(returnUrl);
        }

        ModelState.AddModelError(string.Empty, "İstifadəçi adı və ya şifrə yanlışdır.");
        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Admin/Login.cshtml");
    }

    [HttpPost("logout")]
    [Authorize(Policy = AdminAuthDefaults.Policy)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAdminAsync();
        return Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString("/admin")}");
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
