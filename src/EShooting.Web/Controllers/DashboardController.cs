using EShooting.Application.Sessions.Queries;
using EShooting.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("dashboard")]
public sealed class DashboardController(
    CachedLaneDashboardService laneDashboard,
    IMediator mediator,
    ILogger<DashboardController> logger) : ControllerBase
{
    /// <summary>
    /// Monitor ve admin paneli ucun lane veziyyetlerini cemlenmis sekilde qaytarir.
    /// </summary>
    [HttpGet("lanes")]
    public async Task<IActionResult> GetLanes(CancellationToken cancellationToken)
    {
        try
        {
            var lanes = await laneDashboard.GetLanesAsync(cancellationToken);
            return Ok(lanes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zolaq paneli yüklənmədi.");
            throw;
        }
    }

    /// <summary>Resepsiya üst kartları — yalnız cari gün.</summary>
    [HttpGet("day-stats")]
    public async Task<IActionResult> GetDayStats(CancellationToken cancellationToken)
    {
        try
        {
            var stats = await mediator.Send(new GetReceptionDayStatsQuery(), cancellationToken);
            return Ok(new
            {
                incomingCustomersToday = stats.IncomingCustomersToday,
                activeSessions = stats.ActiveSessions,
                scheduledSessionsToday = stats.ScheduledSessionsToday,
                completedSessionsToday = stats.CompletedSessionsToday
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gün statistikasi yüklənmədi.");
            throw;
        }
    }
}
