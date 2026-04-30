using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

/// <summary>
/// Hər zolaq üçün ayrı tam ekran URL (televizor nümayişi).
/// Məs: /zolaq/3
/// </summary>
[AllowAnonymous]
public sealed class LaneScreenController : Controller
{
    [HttpGet("/zolaq/{laneNumber:int}")]
    public IActionResult Index([FromRoute] int laneNumber)
    {
        if (laneNumber is < 1 or > 11)
        {
            return NotFound();
        }

        return View(laneNumber);
    }
}
