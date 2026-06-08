using EShooting.Application.Common.Models;
using EShooting.Application.Sessions.Queries;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace EShooting.Web.Services;

/// <summary>
/// Zolaq paneli üçün qısa müddətli keş — eyni anda çox monitor sorğusunda DB yüklənməsin.
/// </summary>
public sealed class CachedLaneDashboardService(IMediator mediator, IMemoryCache cache)
{
    private const string CacheKey = "lane-dashboard-items";

    public async Task<IReadOnlyCollection<LaneDashboardItem>> GetLanesAsync(CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return await mediator.Send(new GetLaneDashboardQuery(), cancellationToken);
        }) ?? Array.Empty<LaneDashboardItem>();
    }
}
