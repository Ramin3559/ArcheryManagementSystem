using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;

namespace EShooting.Application.Tests;

internal sealed class InMemoryTrainingCenterRepository : ITrainingCenterRepository
{
    private readonly List<Athlete> _athletes = [];
    private readonly List<Lane> _lanes = [];
    private readonly List<TrainingSession> _sessions = [];
    private readonly List<SubscriptionSchedule> _subscriptionSchedules = [];

    public InMemoryTrainingCenterRepository(
        IEnumerable<Lane>? lanes = null,
        IEnumerable<Athlete>? athletes = null,
        IEnumerable<TrainingSession>? sessions = null,
        IEnumerable<SubscriptionSchedule>? subscriptionSchedules = null)
    {
        if (lanes is not null)
        {
            _lanes.AddRange(lanes);
        }

        if (athletes is not null)
        {
            _athletes.AddRange(athletes);
        }

        if (sessions is not null)
        {
            _sessions.AddRange(sessions);
        }

        if (subscriptionSchedules is not null)
        {
            _subscriptionSchedules.AddRange(subscriptionSchedules);
        }
    }

    public Task<Athlete> AddAthleteAsync(Athlete athlete, CancellationToken cancellationToken)
    {
        _athletes.Add(athlete);
        return Task.FromResult(athlete);
    }

    public Task UpdateAthleteAsync(Athlete athlete, CancellationToken cancellationToken)
    {
        var existing = _athletes.FirstOrDefault(x => x.Id == athlete.Id);
        if (existing is null)
        {
            _athletes.Add(athlete);
            return Task.CompletedTask;
        }

        existing.FullName = athlete.FullName;
        existing.FirstName = athlete.FirstName;
        existing.LastName = athlete.LastName;
        existing.PhoneNumber = athlete.PhoneNumber;
        existing.Email = athlete.Email;
        existing.IdCardNumber = athlete.IdCardNumber;
        existing.Category = athlete.Category;
        existing.IsSubscriber = athlete.IsSubscriber;
        existing.MembershipType = athlete.MembershipType;
        existing.IsFullPackage = athlete.IsFullPackage;
        return Task.CompletedTask;
    }

    public Task<TrainingSession> AddSessionAsync(TrainingSession session, CancellationToken cancellationToken)
    {
        _sessions.Add(session);
        return Task.FromResult(session);
    }

    public Task<TrainingSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_sessions.FirstOrDefault(x => x.Id == sessionId));
    }

    public Task UpdateSessionAsync(TrainingSession session, CancellationToken cancellationToken)
    {
        var existing = _sessions.FirstOrDefault(x => x.Id == session.Id);
        if (existing is null)
        {
            _sessions.Add(session);
            return Task.CompletedTask;
        }

        existing.AthleteId = session.AthleteId;
        existing.LaneId = session.LaneId;
        existing.StartTimeUtc = session.StartTimeUtc;
        existing.EndTimeUtc = session.EndTimeUtc;
        existing.Status = session.Status;
        existing.Scores = session.Scores;
        return Task.CompletedTask;
    }

    public Task<int?> DeleteLastScoreAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = _sessions.FirstOrDefault(x => x.Id == sessionId);
        if (session is null)
        {
            return Task.FromResult<int?>(null);
        }

        if (session.Scores.Count == 0)
        {
            return Task.FromResult<int?>(session.TotalScore);
        }

        var last = session.Scores
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .First();

        session.Scores.Remove(last);
        return Task.FromResult<int?>(session.TotalScore);
    }

    public Task<IReadOnlyCollection<TrainingSession>> GetSessionsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<TrainingSession>>(_sessions);
    }

    public Task<Lane?> GetLaneByNumberAsync(int laneNumber, CancellationToken cancellationToken)
    {
        return Task.FromResult(_lanes.FirstOrDefault(x => x.Number == laneNumber));
    }

    public Task<IReadOnlyCollection<Lane>> GetLanesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<Lane>>(_lanes.OrderBy(x => x.Number).ToList());
    }

    public Task<IReadOnlyCollection<Athlete>> GetAthletesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<Athlete>>(_athletes);
    }

    public Task<SubscriptionSchedule> AddSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken)
    {
        _subscriptionSchedules.Add(schedule);
        return Task.FromResult(schedule);
    }

    public Task UpdateSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken)
    {
        var existing = _subscriptionSchedules.FirstOrDefault(x => x.Id == schedule.Id);
        if (existing is null)
        {
            _subscriptionSchedules.Add(schedule);
            return Task.CompletedTask;
        }

        existing.AthleteId = schedule.AthleteId;
        existing.LaneNumber = schedule.LaneNumber;
        existing.DayOfWeek = schedule.DayOfWeek;
        existing.StartTimeLocal = schedule.StartTimeLocal;
        existing.DurationMinutes = schedule.DurationMinutes;
        existing.ActiveFromDateLocal = schedule.ActiveFromDateLocal;
        existing.ActiveToDateLocal = schedule.ActiveToDateLocal;
        existing.IsEnabled = schedule.IsEnabled;
        existing.LastAssignedLaneNumber = schedule.LastAssignedLaneNumber;
        existing.LastAutoStartedAtUtc = schedule.LastAutoStartedAtUtc;
        existing.CreatedAtUtc = schedule.CreatedAtUtc;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<SubscriptionSchedule>> GetSubscriptionSchedulesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<SubscriptionSchedule>>(_subscriptionSchedules);
    }
}
