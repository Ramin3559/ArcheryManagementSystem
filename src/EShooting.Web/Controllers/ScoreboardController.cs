using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

/// <summary>
/// Legacy/alias routes for scoreboard screens (IIS-friendly).
/// </summary>
[AllowAnonymous]
public sealed class ScoreboardController : Controller
{
    // Alias for older IIS routes like: /Scoreboard/Lane/1
    [HttpGet("/Scoreboard/Lane/{laneNumber:int}")]
    public IActionResult Lane([FromRoute] int laneNumber)
    {
        return Redirect($"/monitor/scoreboard/{laneNumber}");
    }
}

