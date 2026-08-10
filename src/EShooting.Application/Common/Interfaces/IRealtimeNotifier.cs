namespace EShooting.Application.Common.Interfaces;

public interface IRealtimeNotifier
{
    Task PublishLaneUpdateAsync(int laneNumber, CancellationToken cancellationToken);
    Task PublishScoreUpdateAsync(Guid sessionId, int totalScore, CancellationToken cancellationToken);

    /// <summary>
    /// Paket kataloqu dəyişəndə (yarat/dəyiş/aktiv/sil) açıq resepsiya və s. siyahıları yeniləsin.
    /// </summary>
    Task PublishPackagesChangedAsync(CancellationToken cancellationToken);
}
