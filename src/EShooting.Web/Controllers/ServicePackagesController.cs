using EShooting.Application.Common;
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
            billingTypeLabel = ServicePackageRules.IsVipPackage(p.BillingType, p.Scope, p.SchedulingMode, p.SessionDurationMinutes)
                ? "VIP"
                : ServicePackageRules.IsUnlimitedPackage(p.BillingType, p.SchedulingMode, p.SessionDurationMinutes, p.Scope)
                ? "Limitsiz"
                : p.BillingType switch
            {
                PackageBillingType.OneTime => "Birdefəlik",
                PackageBillingType.Monthly => "Aylıq",
                PackageBillingType.Yearly => "İllik",
                PackageBillingType.Vip => "VIP",
                PackageBillingType.Gym => "Trenajor",
                PackageBillingType.Unlimited => "Limitsiz",
                _ => p.BillingType.ToString()
            },
            scope = p.Scope.ToString(),
            scopeLabel = p.Scope switch
            {
                PackageScope.Archery => "Yalnız oxatma",
                PackageScope.Gym => "Yalnız Trenajor",
                PackageScope.Full => "Hər ikisi",
                PackageScope.Vip => "VIP",
                _ => p.Scope.ToString()
            },
            p.SessionDurationMinutes,
            p.PeriodMinutesQuota,
            p.WeeklyDaysCsv,
            p.WeeklyDaysCount,
            p.ValidityDays,
            schedulingMode = p.SchedulingMode.ToString(),
            p.UnlimitedGym,
            p.Price
        });

        return Ok(result);
    }
}
