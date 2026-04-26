using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

public sealed class HomeController : Controller
{
    /// <summary>
    /// Esas MVC idareetme sehifesini render edir.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
