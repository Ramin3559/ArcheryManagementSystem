using EShooting.Web.Contracts.Athletes;
using EShooting.Application.Athletes.Commands;
using EShooting.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("athletes")]
public sealed class AthletesController(IMediator mediator, ITrainingCenterRepository repository) : ControllerBase
{
    /// <summary>
    /// Yeni idmancini qeydiyyatdan kecirir ve yaradilan identifikatoru qaytarir.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterAthleteRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            new RegisterAthleteCommand(request.FullName, request.IsSubscriber),
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
