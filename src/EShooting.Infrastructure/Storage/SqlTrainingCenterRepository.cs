using EShooting.Application.Athletes;
using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.StaffMembers;
using EShooting.Application.Equipment;
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
        existing.ClubCardNumber = athlete.ClubCardNumber;
        existing.Category = athlete.Category;
        existing.IsSubscriber = athlete.IsSubscriber;
        existing.MembershipType = athlete.MembershipType;
        existing.IsFullPackage = athlete.IsFullPackage;
        existing.IsVip = athlete.IsVip;
        existing.IsGroupPlaceholder = athlete.IsGroupPlaceholder;
        existing.IsActive = athlete.IsActive;
        existing.RegisteredByStaffId = athlete.RegisteredByStaffId;

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
        existing.ActivatedAtUtc = session.ActivatedAtUtc;
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

    public async Task<IReadOnlyCollection<TrainingSession>> GetSessionsByLocalDateRangeAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        CancellationToken cancellationToken)
    {
        var from = fromLocalDate.Date;
        var to = toLocalDate.Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var startUtc = DateTime.SpecifyKind(from, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(to.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        return await dbContext.Sessions
            .Include(x => x.Scores)
            .AsNoTracking()
            .Where(x => x.StartTimeUtc >= startUtc && x.StartTimeUtc < endUtc)
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
        CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        var phoneQ = (phoneDigits ?? "").Trim();
        var emailQ = (emailNormalized ?? "").Trim();
        var idQ = (idCardNormalized ?? "").Trim();

        var hasQuery =
            (!string.IsNullOrWhiteSpace(phoneQ) && phoneQ.Length >= 3)
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
            .Where(a => includeInactive || a.IsActive)
            .OrderByDescending(a => Score(a, phoneQ, emailQ, idQ))
            .FirstOrDefault();
    }

    public async Task<Athlete?> FindAthleteByExactPhoneAsync(
        string phoneDigits,
        CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        var normalized = AthleteRegistrationRules.NormalizeDigits(phoneDigits);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var candidates = await dbContext.Athletes
            .AsNoTracking()
            .Where(a => a.PhoneNumber != null && a.PhoneNumber != "")
            .ToListAsync(cancellationToken);

        return candidates
            .Where(a => AthleteRegistrationRules.NormalizeDigits(a.PhoneNumber) == normalized)
            .Where(a => includeInactive || a.IsActive)
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefault();
    }

    public async Task<(Guid SessionId, int LaneNumber)?> TryGetActiveSessionForAthleteAsync(
        Guid athleteId,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(s => s.AthleteId == athleteId && s.Status != SessionStatus.Completed)
            .ToListAsync(cancellationToken);

        var active = sessions.FirstOrDefault(s => SessionHousekeeping.IsAthleteSessionCurrentlyActive(s, nowUtc));

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

    public async Task<IReadOnlyCollection<ServicePackage>> GetServicePackagesAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.ServicePackages.AsNoTracking().Where(x => !x.IsDeleted);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<ServicePackage?> GetServicePackageByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.ServicePackages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<ServicePackage> AddServicePackageAsync(ServicePackage package, CancellationToken cancellationToken)
    {
        await dbContext.ServicePackages.AddAsync(package, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return package;
    }

    public async Task UpdateServicePackageAsync(ServicePackage package, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ServicePackages
            .FirstOrDefaultAsync(x => x.Id == package.Id, cancellationToken)
            ?? throw new InvalidOperationException("Paket tapılmadı.");

        existing.Name = package.Name;
        existing.BillingType = package.BillingType;
        existing.Scope = package.Scope;
        existing.SchedulingMode = package.SchedulingMode;
        existing.Price = package.Price;
        existing.SessionDurationMinutes = package.SessionDurationMinutes;
        existing.PeriodMinutesQuota = package.PeriodMinutesQuota;
        existing.WeeklyDaysCsv = package.WeeklyDaysCsv;
        existing.ValidityDays = package.ValidityDays;
        existing.UnlimitedGym = package.UnlimitedGym;
        existing.IsActive = package.IsActive;
        existing.IsDeleted = package.IsDeleted;
        existing.UpdatedAtUtc = package.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EquipmentItem>> GetEquipmentItemsAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.EquipmentItems.AsNoTracking().Where(x => !x.IsDeleted);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<EquipmentItem?> GetEquipmentItemByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.EquipmentItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<EquipmentItem> AddEquipmentItemAsync(EquipmentItem item, CancellationToken cancellationToken)
    {
        await dbContext.EquipmentItems.AddAsync(item, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task UpdateEquipmentItemAsync(EquipmentItem item, CancellationToken cancellationToken)
    {
        var existing = await dbContext.EquipmentItems
            .FirstOrDefaultAsync(x => x.Id == item.Id, cancellationToken)
            ?? throw new InvalidOperationException("Avadanlıq tapılmadı.");

        existing.Name = item.Name;
        existing.Category = item.Category;
        existing.UsageMode = item.UsageMode;
        existing.RentalQuantity = item.RentalQuantity;
        existing.SaleQuantity = item.SaleQuantity;
        existing.Quantity = item.Quantity;
        existing.DamagedQuantity = item.DamagedQuantity;
        existing.Price = item.Price;
        existing.IsActive = item.IsActive;
        existing.IsDeleted = item.IsDeleted;
        existing.UpdatedAtUtc = item.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SessionEquipmentIssue>> GetSessionEquipmentIssuesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.SessionEquipmentIssues.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddSessionEquipmentIssuesAsync(IReadOnlyCollection<SessionEquipmentIssue> issues, CancellationToken cancellationToken)
    {
        if (issues.Count == 0) return;
        await dbContext.SessionEquipmentIssues.AddRangeAsync(issues, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SessionEquipmentIssue?> GetSessionEquipmentIssueByIdAsync(Guid issueId, CancellationToken cancellationToken)
    {
        return dbContext.SessionEquipmentIssues.FirstOrDefaultAsync(x => x.Id == issueId, cancellationToken);
    }

    public async Task UpdateSessionEquipmentIssueAsync(SessionEquipmentIssue issue, CancellationToken cancellationToken)
    {
        var existing = await dbContext.SessionEquipmentIssues
            .FirstOrDefaultAsync(x => x.Id == issue.Id, cancellationToken)
            ?? throw new InvalidOperationException("Avadanlıq verilməsi tapılmadı.");

        existing.ReturnedAtUtc = issue.ReturnedAtUtc;
        existing.ReturnedByStaffId = issue.ReturnedByStaffId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EquipmentSaleReceipt>> GetEquipmentSaleReceiptsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.EquipmentSaleReceipts.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EquipmentSaleReceiptLine>> GetEquipmentSaleReceiptLinesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.EquipmentSaleReceiptLines.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<EquipmentSaleReceipt?> GetEquipmentSaleReceiptByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.EquipmentSaleReceipts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddEquipmentSaleReceiptAsync(
        EquipmentSaleReceipt receipt,
        IReadOnlyCollection<EquipmentSaleReceiptLine> lines,
        CancellationToken cancellationToken)
    {
        await dbContext.EquipmentSaleReceipts.AddAsync(receipt, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (lines.Count > 0)
        {
            foreach (var line in lines)
            {
                line.ReceiptId = receipt.Id;
                await dbContext.EquipmentSaleReceiptLines.AddAsync(line, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task CreateEquipmentSaleAsync(
        EquipmentSaleReceipt receipt,
        IReadOnlyCollection<EquipmentSaleReceiptLine> lines,
        IReadOnlyDictionary<Guid, int> quantitySoldByItemId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var (itemId, qty) in quantitySoldByItemId)
            {
                var item = await dbContext.EquipmentItems
                    .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken)
                    ?? throw new InvalidOperationException("Avadanlıq tapılmadı.");

                EquipmentIssuanceRules.ApplyStockOnIssue(item, EquipmentIssueType.Sale, qty);
            }

            await dbContext.EquipmentSaleReceipts.AddAsync(receipt, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var line in lines)
            {
                line.ReceiptId = receipt.Id;
                await dbContext.EquipmentSaleReceiptLines.AddAsync(line, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateEquipmentSaleReceiptAsync(EquipmentSaleReceipt receipt, CancellationToken cancellationToken)
    {
        dbContext.EquipmentSaleReceipts.Update(receipt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<StaffPosition>> GetStaffPositionsAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.StaffPositions.AsNoTracking().Where(x => !x.IsDeleted);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<StaffPosition?> GetStaffPositionByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.StaffPositions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<StaffPosition> AddStaffPositionAsync(StaffPosition position, CancellationToken cancellationToken)
    {
        await dbContext.StaffPositions.AddAsync(position, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return position;
    }

    public async Task UpdateStaffPositionAsync(StaffPosition position, CancellationToken cancellationToken)
    {
        var existing = await dbContext.StaffPositions
            .FirstOrDefaultAsync(x => x.Id == position.Id, cancellationToken)
            ?? throw new InvalidOperationException("Vəzifə tapılmadı.");

        existing.Name = position.Name;
        existing.Description = position.Description;
        existing.DefaultAccessProfileId = position.DefaultAccessProfileId;
        existing.IsActive = position.IsActive;
        existing.IsDeleted = position.IsDeleted;
        existing.UpdatedAtUtc = position.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AccessProfile>> GetAccessProfilesAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.AccessProfiles.AsNoTracking().Where(x => !x.IsDeleted);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<AccessProfile?> GetAccessProfileByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.AccessProfiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<AccessProfile> AddAccessProfileAsync(AccessProfile profile, CancellationToken cancellationToken)
    {
        await dbContext.AccessProfiles.AddAsync(profile, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task UpdateAccessProfileAsync(AccessProfile profile, CancellationToken cancellationToken)
    {
        var existing = await dbContext.AccessProfiles
            .FirstOrDefaultAsync(x => x.Id == profile.Id, cancellationToken)
            ?? throw new InvalidOperationException("İcazə profili tapılmadı.");

        existing.Name = profile.Name;
        existing.Description = profile.Description;
        existing.CanRegisterCustomers = profile.CanRegisterCustomers;
        existing.CanViewCustomerDetails = profile.CanViewCustomerDetails;
        existing.CanEditCustomerDetails = profile.CanEditCustomerDetails;
        existing.CanManageSubscriptions = profile.CanManageSubscriptions;
        existing.CanRecordPayments = profile.CanRecordPayments;
        existing.CanApplyDiscount = profile.CanApplyDiscount;
        existing.CanGrantComplimentarySession = profile.CanGrantComplimentarySession;
        existing.CanManageSessions = profile.CanManageSessions;
        existing.CanManageEquipment = profile.CanManageEquipment;
        existing.CanSellEquipment = profile.CanSellEquipment;
        existing.CanReturnEquipment = profile.CanReturnEquipment;
        existing.CanAccessPlanset = profile.CanAccessPlanset;
        existing.CanIssueEquipmentRental = profile.CanIssueEquipmentRental;
        existing.CanViewHistory = profile.CanViewHistory;
        existing.IsActive = profile.IsActive;
        existing.IsDeleted = profile.IsDeleted;
        existing.UpdatedAtUtc = profile.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<StaffMember>> GetStaffMembersAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = dbContext.StaffMembers
            .AsNoTracking()
            .Include(x => x.StaffPosition)
            .Include(x => x.AccessProfile)
            .AsQueryable();

        query = query.Where(x => !x.IsDeleted);

        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<StaffMember?> GetStaffMemberByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.StaffMembers
            .Include(x => x.StaffPosition)
            .Include(x => x.AccessProfile)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<StaffMember?> GetStaffMemberByPinAsync(string pin, CancellationToken cancellationToken)
    {
        var hash = StaffPinHasher.Hash(pin);
        return await dbContext.StaffMembers
            .AsNoTracking()
            .Include(x => x.StaffPosition)
            .Include(x => x.AccessProfile)
            .FirstOrDefaultAsync(
                x => x.PinHash == hash && x.IsActive && !x.IsDeleted,
                cancellationToken);
    }

    public async Task<StaffMember> AddStaffMemberAsync(StaffMember member, CancellationToken cancellationToken)
    {
        await dbContext.StaffMembers.AddAsync(member, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return member;
    }

    public async Task UpdateStaffMemberAsync(StaffMember member, CancellationToken cancellationToken)
    {
        var existing = await dbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == member.Id, cancellationToken)
            ?? throw new InvalidOperationException("İşçi tapılmadı.");

        existing.FirstName = member.FirstName;
        existing.LastName = member.LastName;
        existing.StaffPositionId = member.StaffPositionId;
        existing.AccessProfileId = member.AccessProfileId;
        existing.PhoneNumber = member.PhoneNumber;
        existing.PinHash = member.PinHash;
        existing.PinPlain = member.PinPlain;
        existing.IsActive = member.IsActive;
        existing.IsDeleted = member.IsDeleted;
        existing.UpdatedAtUtc = member.UpdatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsStaffPinInUseAsync(string pin, Guid? excludeMemberId, CancellationToken cancellationToken)
    {
        var hash = StaffPinHasher.Hash(pin);
        var query = dbContext.StaffMembers.AsNoTracking().Where(x => x.PinHash == hash);
        if (excludeMemberId is not null && excludeMemberId != Guid.Empty)
        {
            query = query.Where(x => x.Id != excludeMemberId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> IsStaffPhoneInUseAsync(string phoneNumber, Guid? excludeMemberId, CancellationToken cancellationToken)
    {
        var phone = (phoneNumber ?? "").Trim();
        var query = dbContext.StaffMembers.AsNoTracking().Where(x => x.PhoneNumber != null && x.PhoneNumber == phone);
        if (excludeMemberId is not null && excludeMemberId != Guid.Empty)
        {
            query = query.Where(x => x.Id != excludeMemberId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<CustomerPackageRecord> AddCustomerPackageRecordAsync(CustomerPackageRecord record, CancellationToken cancellationToken)
    {
        await dbContext.CustomerPackageRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    public async Task<CustomerPackageRecord?> GetCustomerPackageRecordByIdAsync(Guid id, CancellationToken cancellationToken)
        => await dbContext.CustomerPackageRecords.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task UpdateCustomerPackageRecordAsync(CustomerPackageRecord record, CancellationToken cancellationToken)
    {
        dbContext.CustomerPackageRecords.Update(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CustomerPackageRecord>> GetCustomerPackageRecordsAsync(CancellationToken cancellationToken)
        => await dbContext.CustomerPackageRecords.AsNoTracking().ToListAsync(cancellationToken);
}
