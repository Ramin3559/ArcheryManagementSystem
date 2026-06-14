using EShooting.Application.Common.Models;
using EShooting.Application.Sessions.Queries;
using MediatR;

namespace EShooting.Web.Services;

/// <summary>
/// Zolaq paneli üçün lane vəziyyətlərini qaytarır (qeydiyyatdan dərhal sonra yenilənmə üçün keş yoxdur).
/// </summary>
public sealed class CachedLaneDashboardService(IMediator mediator)
{
    public Task<IReadOnlyCollection<LaneDashboardItem>> GetLanesAsync(CancellationToken cancellationToken)
    {
        return mediator.Send(new GetLaneDashboardQuery(), cancellationToken);
    }
}
