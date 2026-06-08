using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[AllowAnonymous]
public sealed class TabletController : Controller
{
    [HttpGet("/tablet")]
    public IActionResult Index([FromQuery] int? lane)
    {
        if (lane is < 1 or > 11)
        {
            lane = null;
        }

        ViewData["InitialLane"] = lane;
        return View();
    }
}

