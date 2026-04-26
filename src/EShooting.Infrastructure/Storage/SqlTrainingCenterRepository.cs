using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EShooting.Infrastructure.Storage;

public sealed class SqlTrainingCenterRepository(EShootingDbContext dbContext) : ITrainingCenterRepository
{
    public async Task<Athlete> AddAthleteAsync(Athlete athlete, CancellationToken cancellationToken)
    {
        await dbContext.Athletes.AddAsync(athlete, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return athlete;
    }

    public async Task UpdateAthleteAsync(Athlete athlete, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Athletes
            .FirstOrDefaultAsync(x => x.Id == athlete.Id, cancellationToken)
            ?? throw new InvalidOperationException("Athlete not found.");

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

        await dbContext.SaveChangesAsync(cancellationToken);
    }
   
    public async Task<TrainingSession> AddSessionAsync(TrainingSession session, CancellationToken cancellationToken)
    {
        await dbContext.Sessions.AddAsync(session, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public Task<TrainingSession?> GetSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return dbContext.Sessions
            .Include(x => x.Scores)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
    }
    public async Task UpdateSessionAsync(TrainingSession session, CancellationToken cancellationToken)
    {
        var trackedState = dbContext.Entry(session).State;
        if (trackedState is not EntityState.Detached)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var existing = await dbContext.Sessions
            .Include(x => x.Scores)
            .FirstOrDefaultAsync(x => x.Id == session.Id, cancellationToken)
            ?? throw new InvalidOperationException("Session not found.");

        existing.AthleteId = session.AthleteId;
        existing.LaneId = session.LaneId;
        existing.StartTimeUtc = session.StartTimeUtc;
        existing.EndTimeUtc = session.EndTimeUtc;
        existing.Status = session.Status;
        existing.SubscriptionScheduleId = session.SubscriptionScheduleId;
        existing.IsEquipmentIssued = session.IsEquipmentIssued;
        existing.EquipmentReturnedAtUtc = session.EquipmentReturnedAtUtc;

        foreach (var score in session.Scores)
        {
            var existingScore = existing.Scores.FirstOrDefault(x => x.Id == score.Id);
            if (existingScore is null)
            {
                existing.Scores.Add(new ScoreEntry
                {
                    Id = score.Id,
                    SessionId = existing.Id,
                    RoundNumber = score.RoundNumber,
                    Value = score.Value,
                    CreatedAtUtc = score.CreatedAtUtc
                });
                continue;
            }

            existingScore.RoundNumber = score.RoundNumber;
            existingScore.Value = score.Value;
            existingScore.CreatedAtUtc = score.CreatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int?> DeleteLastScoreAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .Include(x => x.Scores)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        if (session.Scores.Count == 0)
        {
            return session.TotalScore;
        }

        var last = session.Scores
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .First();

        dbContext.Scores.Remove(last);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session.TotalScore;
    }

    public async Task<IReadOnlyCollection<TrainingSession>> GetSessionsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Sessions
            .Include(x => x.Scores)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Lane?> GetLaneByNumberAsync(int laneNumber, CancellationToken cancellationToken)
    {
        return dbContext.Lanes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Number == laneNumber, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Lane>> GetLanesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Lanes
            .AsNoTracking()
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Athlete>> GetAthletesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Athletes
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriptionSchedule> AddSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken)
    {
        await dbContext.SubscriptionSchedules.AddAsync(schedule, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return schedule;
    }

    public async Task UpdateSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken)
    {
        var trackedState = dbContext.Entry(schedule).State;
        if (trackedState is not EntityState.Detached)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var existing = await dbContext.SubscriptionSchedules
            .FirstOrDefaultAsync(x => x.Id == schedule.Id, cancellationToken)
            ?? throw new InvalidOperationException("Subscription schedule not found.");

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

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SubscriptionSchedule>> GetSubscriptionSchedulesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.SubscriptionSchedules
            .AsNoTracking()
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTimeLocal)
            .ToListAsync(cancellationToken);
    }
}
