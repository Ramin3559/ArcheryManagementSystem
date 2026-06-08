using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
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

    public async Task<IReadOnlyCollection<TrainingSession>> GetSessionsLightAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Sessions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Athlete?> GetAthleteByIdAsync(Guid athleteId, CancellationToken cancellationToken)
    {
        return dbContext.Athletes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == athleteId, cancellationToken);
    }

    public async Task<Athlete?> FindAthleteForLookupAsync(
        string phoneDigits,
        string emailNormalized,
        string idCardNormalized,
        CancellationToken cancellationToken)
    {
        var phoneQ = (phoneDigits ?? "").Trim();
        var emailQ = (emailNormalized ?? "").Trim();
        var idQ = (idCardNormalized ?? "").Trim();

        var hasQuery =
            (!string.IsNullOrWhiteSpace(phoneQ) && phoneQ.Length >= 4)
            || (!string.IsNullOrWhiteSpace(emailQ) && emailQ.Length >= 4)
            || (!string.IsNullOrWhiteSpace(idQ) && idQ.Length >= 3);

        if (!hasQuery)
        {
            return null;
        }

        var candidates = await dbContext.Athletes
            .AsNoTracking()
            .Where(a =>
                (string.IsNullOrEmpty(phoneQ)
                 || (a.PhoneNumber != null && a.PhoneNumber.Contains(phoneQ)))
                && (string.IsNullOrEmpty(emailQ)
                    || (a.Email != null && a.Email.ToLower().Contains(emailQ)))
                && (string.IsNullOrEmpty(idQ)
                    || (a.IdCardNumber != null && a.IdCardNumber.ToLower().Contains(idQ))))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return null;
        }

        static int Score(Athlete a, string pQ, string eQ, string iQ)
        {
            var score = 0;
            var phone = string.IsNullOrWhiteSpace(a.PhoneNumber)
                ? ""
                : new string(a.PhoneNumber.Where(char.IsDigit).ToArray());
            var email = (a.Email ?? "").Trim().ToLowerInvariant();
            var id = (a.IdCardNumber ?? "").Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(pQ) && phone == pQ) score += 100;
            if (!string.IsNullOrWhiteSpace(iQ) && id == iQ) score += 90;
            if (!string.IsNullOrWhiteSpace(eQ) && email == eQ) score += 80;
            if (!string.IsNullOrWhiteSpace(pQ) && phone.Contains(pQ, StringComparison.Ordinal)) score += Math.Min(30, pQ.Length);
            if (!string.IsNullOrWhiteSpace(iQ) && id.Contains(iQ, StringComparison.Ordinal)) score += Math.Min(20, iQ.Length);
            if (!string.IsNullOrWhiteSpace(eQ) && email.Contains(eQ, StringComparison.Ordinal)) score += Math.Min(25, eQ.Length);
            return score;
        }

        return candidates
            .OrderByDescending(a => Score(a, phoneQ, emailQ, idQ))
            .FirstOrDefault();
    }

    public async Task<(Guid SessionId, int LaneNumber)?> TryGetActiveSessionForAthleteAsync(
        Guid athleteId,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.AthleteId == athleteId && s.Status == SessionStatus.Active)
            .ToListAsync(cancellationToken);

        var active = sessions.FirstOrDefault(s =>
        {
            var start = s.StartTimeUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(s.StartTimeUtc, DateTimeKind.Utc)
                : s.StartTimeUtc.ToUniversalTime();
            var end = s.EndTimeUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(s.EndTimeUtc, DateTimeKind.Utc)
                : s.EndTimeUtc.ToUniversalTime();
            return start <= nowUtc && nowUtc < end;
        });

        if (active is null)
        {
            return null;
        }

        var laneNumber = await dbContext.Lanes
            .AsNoTracking()
            .Where(l => l.Id == active.LaneId)
            .Select(l => l.Number)
            .FirstOrDefaultAsync(cancellationToken);

        return (active.Id, laneNumber);
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
        existing.PreferredLaneType = schedule.PreferredLaneType;
        existing.IsFullPackage = schedule.IsFullPackage;
        existing.LastAssignedLaneNumber = schedule.LastAssignedLaneNumber;
        existing.LastAutoStartedAtUtc = schedule.LastAutoStartedAtUtc;
        existing.CreatedAtUtc = schedule.CreatedAtUtc;
        existing.ExcludedOccurrenceDatesJson = schedule.ExcludedOccurrenceDatesJson;
        existing.OccurrenceOverridesJson = schedule.OccurrenceOverridesJson;

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
