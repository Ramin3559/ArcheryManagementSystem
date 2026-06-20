using EShooting.Application.Packages.Queries;
using EShooting.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("packages")]
public sealed class ServicePackagesController(IMediator mediator) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var packages = await mediator.Send(new GetServicePackagesQuery(ActiveOnly: true), cancellationToken);
        var result = packages.Select(p => new
        {
            p.Id,
            p.Name,
            billingType = p.BillingType.ToString(),
            billingTypeLabel = p.BillingType switch
            {
                PackageBillingType.OneTime => "Birdefəlik",
                PackageBillingType.Monthly => "Aylıq",
                PackageBillingType.Yearly => "İllik",
                _ => p.BillingType.ToString()
            },
            scope = p.Scope.ToString(),
            p.SessionDurationMinutes,
            p.PeriodMinutesQuota,
            p.WeeklyDaysCsv,
            p.ValidityDays,
            schedulingMode = p.SchedulingMode.ToString(),
            p.UnlimitedGym,
            p.Price
        });

        return Ok(result);
    }
}
