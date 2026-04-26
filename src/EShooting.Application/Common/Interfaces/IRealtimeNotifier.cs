namespace EShooting.Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task PublishLaneUpdateAsync(int laneNumber, CancellationToken cancellationToken);
    Task PublishScoreUpdateAsync(Guid sessionId, int totalScore, CancellationToken cancellationToken);
}
