using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

public sealed class HomeController : Controller
{
    /// <summary>
    /// Kök URL — əlfəcinlər (yalnız IP) birbaşa resepsiya panelinə getsin.
    /// </summary>
    [HttpGet("/")]
    public IActionResult Root()
    {
        return Redirect("/qeydiyyat");
    }

    [HttpGet("/basla")]
    public IActionResult Landing()
    {
        return View();
    }

    /// <summary>
    /// Resepsiya / idarəetmə paneli.
    /// </summary>
    [HttpGet("/qeydiyyat")]
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Köhnə əlfəcinlər /Home/Index üçün.
    /// </summary>
    [HttpGet("/Home/Index")]
    public IActionResult LegacyIndex()
    {
        return RedirectPermanent("/qeydiyyat");
    }
}
