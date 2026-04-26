using EShooting.Web.Hubs;
using EShooting.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace EShooting.Web.Realtime;

public sealed class SignalRRealtimeNotifier(IHubContext<LaneHub> hubContext) : IRealtimeNotifier
{
    /// <summary>
    /// Lane veziyyeti yenileme event-ini butun klientlere gonderir.
    /// </summary>
    public async Task PublishLaneUpdateAsync(int laneNumber, CancellationToken cancellationToken)
    {
        await hubContext.Clients.All
            .SendAsync("lane-updated", laneNumber, cancellationToken);
    }

    /// <summary>
    /// Sessiya uzre umumi xal yenilemesini butun klientlere gonderir.
    /// </summary>
    public async Task PublishScoreUpdateAsync(Guid sessionId, int totalScore, CancellationToken cancellationToken)
    {
        await hubContext.Clients.All.SendAsync("score-updated", new
        {
            SessionId = sessionId,
            TotalScore = totalScore
        }, cancellationToken);
    }
}
