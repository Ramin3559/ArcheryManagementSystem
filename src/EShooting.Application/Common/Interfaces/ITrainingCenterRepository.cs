using EShooting.Domain.Entities;

namespace EShooting.Application.Common.Interfaces;

public interface ITrainingCenterRepository
{
    Task<Athlete> AddAthleteAsync(Athlete athlete, CancellationToken cancellationToken);
    Task UpdateAthleteAsync(Athlete athlete, CancellationToken cancellationToken);
    Task<TrainingSession> AddSessionAsync(TrainingSession session, CancellationToken cancellationToken);
    Task<TrainingSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken);
    Task UpdateSessionAsync(TrainingSession session, CancellationToken cancellationToken);
    Task<int?> DeleteLastScoreAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TrainingSession>> GetSessionsAsync(CancellationToken cancellationToken);
    /// <summary>Sessiya planlaması üçün — xal cədvəli olmadan, daha sürətli.</summary>
    Task<IReadOnlyCollection<TrainingSession>> GetSessionsLightAsync(CancellationToken cancellationToken);
    Task<Lane?> GetLaneByNumberAsync(int laneNumber, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Lane>> GetLanesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Athlete>> GetAthletesAsync(CancellationToken cancellationToken);
    Task<Athlete?> GetAthleteByIdAsync(Guid athleteId, CancellationToken cancellationToken);
    Task<Athlete?> FindAthleteForLookupAsync(string phoneDigits, string emailNormalized, string idCardNormalized, CancellationToken cancellationToken);
    Task<(Guid SessionId, int LaneNumber)?> TryGetActiveSessionForAthleteAsync(Guid athleteId, CancellationToken cancellationToken);
    Task<SubscriptionSchedule> AddSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken);
    Task UpdateSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SubscriptionSchedule>> GetSubscriptionSchedulesAsync(CancellationToken cancellationToken);
}
