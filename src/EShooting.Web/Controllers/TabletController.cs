using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[AllowAnonymous]
public sealed class TabletController : Controller
{
    [HttpGet("/tablet")]
    public IActionResult Index()
    {
        return View();
    }
}

