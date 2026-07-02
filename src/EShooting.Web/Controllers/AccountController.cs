using System.Security.Claims;
using EShooting.Web.Auth;
using EShooting.Web.Extensions;
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
        if (IsReceptionReturnUrl(returnUrl))
        {
            return Redirect("/resepsiya/giris");
        }

        var isAdminReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

        if (await HttpContext.IsAdminAuthenticatedAsync())
        {
            if (isAdminReturnUrl || string.IsNullOrWhiteSpace(returnUrl))
            {
                return RedirectToLocal(returnUrl ?? "/admin");
            }

            return Redirect("/admin");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            if (!User.IsInRole("Reception") && !User.IsInRole("ReceptionStaff"))
            {
                await HttpContext.SignOutReceptionAsync();
            }
            else if (isAdminReturnUrl)
            {
                ViewData["ReturnUrl"] = returnUrl;
                ViewData["AdminLoginRequired"] = true;
                return View();
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
            var identity = new ClaimsIdentity(
                claims,
                AdminAuthDefaults.Scheme,
                ClaimTypes.Name,
                ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAdminAsync(principal);

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

    [HttpGet]
    [AllowAnonymous]
    public Task<IActionResult> Logout(string? returnUrl = null) => SignOutAdminAndRedirect(returnUrl);

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> LogoutPost(string? returnUrl = null) => SignOutAdminAndRedirect(returnUrl);

    private async Task<IActionResult> SignOutAdminAndRedirect(string? returnUrl)
    {
        await HttpContext.SignOutAdminAsync();
        return RedirectToAction(nameof(Login), new { returnUrl });
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/admin");
    }

    private static bool IsReceptionReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && (returnUrl.StartsWith("/qeydiyyat", StringComparison.OrdinalIgnoreCase)
            || returnUrl.StartsWith("/resepsiya", StringComparison.OrdinalIgnoreCase)
            || returnUrl.StartsWith("/Home/Index", StringComparison.OrdinalIgnoreCase));
}
