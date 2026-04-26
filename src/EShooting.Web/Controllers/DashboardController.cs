    using EShooting.Application.Sessions.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("dashboard")]
public sealed class DashboardController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Monitor ve admin paneli ucun lane veziyyetlerini cemlenmis sekilde qaytarir.
    /// </summary>
    [HttpGet("lanes")]
    public async Task<IActionResult> GetLanes(CancellationToken cancellationToken)
    {
        var lanes = await mediator.Send(new GetLaneDashboardQuery(), cancellationToken);
        return Ok(lanes);
    }
}
