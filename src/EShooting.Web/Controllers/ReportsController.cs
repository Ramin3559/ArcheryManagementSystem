using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[Authorize]
[ApiController]
[Route("reports")]
public sealed class ReportsController(ITrainingCenterRepository repository) : ControllerBase
{
    /// <summary>
    /// Membership type üzrə cari paylanma (sadə hesabat).
    /// </summary>
    [HttpGet("membership-types")]
    public async Task<IActionResult> MembershipTypes(CancellationToken cancellationToken)
    {
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var groups = athletes
            .GroupBy(x => x.MembershipType)
            .ToDictionary(x => x.Key, x => x.Count());

        return Ok(new
        {
            gymOnly = groups.GetValueOrDefault(MembershipType.GymOnly, 0),
            archeryOnly = groups.GetValueOrDefault(MembershipType.ArcheryOnly, 0),
            fullCombo = groups.GetValueOrDefault(MembershipType.FullCombo, 0),
            total = athletes.Count
        });
    }
}

