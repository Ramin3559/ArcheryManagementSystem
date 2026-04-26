using EShooting.Application.Common.Interfaces;

namespace EShooting.Application.Tests;

internal sealed class SpyRealtimeNotifier : IRealtimeNotifier
{
    public List<int> LaneUpdates { get; } = [];
    public List<(Guid SessionId, int TotalScore)> ScoreUpdates { get; } = [];

    public Task PublishLaneUpdateAsync(int laneNumber, CancellationToken cancellationToken)
    {
        LaneUpdates.Add(laneNumber);
        return Task.CompletedTask;
    }

    public Task PublishScoreUpdateAsync(Guid sessionId, int totalScore, CancellationToken cancellationToken)
    {
        ScoreUpdates.Add((sessionId, totalScore));
        return Task.CompletedTask;
    }
}
