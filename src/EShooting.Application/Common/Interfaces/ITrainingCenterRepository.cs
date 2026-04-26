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
    Task<Lane?> GetLaneByNumberAsync(int laneNumber, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Lane>> GetLanesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Athlete>> GetAthletesAsync(CancellationToken cancellationToken);
    Task<SubscriptionSchedule> AddSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken);
    Task UpdateSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SubscriptionSchedule>> GetSubscriptionSchedulesAsync(CancellationToken cancellationToken);
}
