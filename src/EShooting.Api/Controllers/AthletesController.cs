using EShooting.Api.Contracts.Athletes;
using EShooting.Application.Athletes.Commands;
using EShooting.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Api.Controllers;

[ApiController]
[Route("athletes")]
public sealed class AthletesController(IMediator mediator, ITrainingCenterRepository repository) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterAthleteRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            new RegisterAthleteCommand(
                request.FirstName,
                request.LastName,
                request.PhoneNumber,
                request.Email,
                request.IdCardNumber,
                request.ClubCardNumber,
                request.Category,
                request.IsSubscriber,
                request.MembershipType,
                request.IsVip),
            cancellationToken);

        return Ok(new { id });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var result = athletes
            .OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.IsSubscriber
            });

        return Ok(result);
    }
}
