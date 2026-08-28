using EShooting.Application.Athletes.Queries;
using EShooting.Domain.Enums;
using EShooting.Web.Auth;
using EShooting.Web.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("club-cards")]
public sealed class ClubCardsController(IMediator mediator) : ControllerBase
{
    private IActionResult? DenyIfCannotView()
    {
        if (!User.HasAnyReceptionPermission(
                ReceptionStaffClaims.CanManageSessions,
                ReceptionStaffClaims.CanRegisterCustomers,
                ReceptionStaffClaims.CanEditCustomerDetails))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu əməliyyat üçün icazəniz yoxdur." });
        }

        return null;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        if (DenyIfCannotView() is { } denied) return denied;
        var summary = await mediator.Send(new GetClubCardStockSummaryQuery(), cancellationToken);
        return Ok(summary);
    }

    [HttpGet("held")]
    public async Task<IActionResult> Held(CancellationToken cancellationToken)
    {
        if (DenyIfCannotView() is { } denied) return denied;
        var held = await mediator.Send(new GetHeldClubCardsQuery(), cancellationToken);
        return Ok(held);
    }

    [HttpGet("available")]
    public async Task<IActionResult> Available(
        [FromQuery] ClubCardType cardType,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        if (DenyIfCannotView() is { } denied) return denied;
        var numbers = await mediator.Send(
            new GetAvailableClubCardNumbersQuery(cardType, q, 20),
            cancellationToken);
        return Ok(numbers);
    }
}
