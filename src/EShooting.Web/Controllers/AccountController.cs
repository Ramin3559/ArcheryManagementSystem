using System.Security.Claims;
using EShooting.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EShooting.Web.Controllers;

public sealed class AccountController(
    IOptions<ReceptionAuthOptions> options,
    IAdminCredentialStore adminCredentials) : Controller
{
    private readonly ReceptionAuthOptions _auth = options.Value;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var isAdminReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

        if (User.Identity?.IsAuthenticated == true)
        {
            if (!User.IsInRole("Reception") && !User.IsInRole("ReceptionStaff") && !User.IsInRole("Admin"))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            else if (isAdminReturnUrl && !User.IsInRole("Admin"))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Redirect($"/admin/login?returnUrl={Uri.EscapeDataString(returnUrl!)}");
            }
            else
            {
                return RedirectToLocal(returnUrl);
            }
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string userName, string password, string? returnUrl)
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

            return RedirectToLocal(string.IsNullOrWhiteSpace(returnUrl) ? "/admin" : returnUrl);
        }

        if (string.Equals(userName, _auth.UserName, StringComparison.Ordinal)
            && string.Equals(password, _auth.Password, StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                string.Empty,
                "Köhnə resepsiya girişi deaktivdir. PIN ilə /resepsiya/giris səhifəsindən daxil olun.");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        ModelState.AddModelError(string.Empty, "İstifadəçi adı və ya şifrə yanlışdır.");
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(
        string currentPassword,
        string newPassword,
        string confirmPassword,
        string? returnUrl)
    {
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Yeni şifrələr uyğun gəlmir.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(nameof(Login));
        }

        if (!adminCredentials.TryChangePassword(currentPassword, newPassword, out var error))
        {
            ModelState.AddModelError(string.Empty, error ?? "Parol dəyişdirilmədi.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(nameof(Login));
        }

        TempData["PasswordChanged"] = "Parol uğurla dəyişdirildi. Yeni şifrə ilə daxil ola bilərsiniz.";
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    [HttpPost]
    [Authorize]
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
