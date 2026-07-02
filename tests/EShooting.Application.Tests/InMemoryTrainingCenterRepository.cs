using EShooting.Application.Common.Interfaces;
using EShooting.Application.StaffMembers;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

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
        existing.ClubCardNumber = athlete.ClubCardNumber;
        existing.Category = athlete.Category;
        existing.IsSubscriber = athlete.IsSubscriber;
        existing.MembershipType = athlete.MembershipType;
        existing.IsFullPackage = athlete.IsFullPackage;
        existing.IsVip = athlete.IsVip;
        existing.IsActive = athlete.IsActive;
        existing.RegisteredByStaffId = athlete.RegisteredByStaffId;
        return Task.CompletedTask;
    }

    private readonly List<CustomerPackageRecord> _customerPackageRecords = [];

    public Task<CustomerPackageRecord> AddCustomerPackageRecordAsync(CustomerPackageRecord record, CancellationToken cancellationToken)
    {
        _customerPackageRecords.Add(record);
        return Task.FromResult(record);
    }

    public Task<CustomerPackageRecord?> GetCustomerPackageRecordByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_customerPackageRecords.FirstOrDefault(x => x.Id == id));

    public Task UpdateCustomerPackageRecordAsync(CustomerPackageRecord record, CancellationToken cancellationToken)
    {
        var existing = _customerPackageRecords.FirstOrDefault(x => x.Id == record.Id);
        if (existing is null)
        {
            _customerPackageRecords.Add(record);
            return Task.CompletedTask;
        }

        existing.AmountPaidCash = record.AmountPaidCash;
        existing.AmountPaidCard = record.AmountPaidCard;
        existing.AmountPaid = record.AmountPaid;
        existing.PriceDue = record.PriceDue;
        existing.IsComplimentary = record.IsComplimentary;
        existing.IsActive = record.IsActive;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<CustomerPackageRecord>> GetCustomerPackageRecordsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<CustomerPackageRecord>>(_customerPackageRecords.ToList());

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

    public Task<IReadOnlyCollection<TrainingSession>> GetSessionsLightAsync(CancellationToken cancellationToken)
    {
        return GetSessionsAsync(cancellationToken);
    }

    public Task<IReadOnlyCollection<TrainingSession>> GetSessionsByLocalDateRangeAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        CancellationToken cancellationToken)
    {
        var from = fromLocalDate.Date;
        var to = toLocalDate.Date;
        var filtered = _sessions.Where(s =>
        {
            var local = s.StartTimeUtc.ToLocalTime().Date;
            return local >= from && local <= to;
        }).ToList();
        return Task.FromResult<IReadOnlyCollection<TrainingSession>>(filtered);
    }

    public Task<Athlete?> GetAthleteByIdAsync(Guid athleteId, CancellationToken cancellationToken)
    {
        return Task.FromResult(_athletes.FirstOrDefault(x => x.Id == athleteId));
    }

    public Task<Athlete?> FindAthleteForLookupAsync(
        string phoneDigits,
        string emailNormalized,
        string idCardNormalized,
        CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        var phoneQ = (phoneDigits ?? "").Trim();
        var emailQ = (emailNormalized ?? "").Trim();
        var idQ = (idCardNormalized ?? "").Trim();

        var match = _athletes
            .Where(a => includeInactive || a.IsActive)
            .FirstOrDefault(a =>
        {
            var p = string.IsNullOrWhiteSpace(a.PhoneNumber) ? "" : new string(a.PhoneNumber.Where(char.IsDigit).ToArray());
            var e = (a.Email ?? "").Trim().ToLowerInvariant();
            var id = (a.IdCardNumber ?? "").Trim().ToLowerInvariant();
            var phoneOk = string.IsNullOrEmpty(phoneQ) || (!string.IsNullOrEmpty(p) && p.Contains(phoneQ, StringComparison.Ordinal));
            var emailOk = string.IsNullOrEmpty(emailQ) || (!string.IsNullOrEmpty(e) && e.Contains(emailQ, StringComparison.Ordinal));
            var idOk = string.IsNullOrEmpty(idQ) || (!string.IsNullOrEmpty(id) && id.Contains(idQ, StringComparison.Ordinal));
            return phoneOk && emailOk && idOk;
        });

        return Task.FromResult(match);
    }

    public Task<(Guid SessionId, int LaneNumber)?> TryGetActiveSessionForAthleteAsync(
        Guid athleteId,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var active = _sessions.FirstOrDefault(s =>
            s.AthleteId == athleteId
            && s.Status == SessionStatus.Active
            && s.StartTimeUtc <= nowUtc
            && nowUtc < s.EndTimeUtc);

        if (active is null)
        {
            return Task.FromResult<(Guid SessionId, int LaneNumber)?>(null);
        }

        var laneNumber = _lanes.FirstOrDefault(l => l.Id == active.LaneId)?.Number ?? 0;
        return Task.FromResult<(Guid SessionId, int LaneNumber)?>((active.Id, laneNumber));
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
        existing.PreferredLaneType = schedule.PreferredLaneType;
        existing.IsFullPackage = schedule.IsFullPackage;
        existing.ExcludedOccurrenceDatesJson = schedule.ExcludedOccurrenceDatesJson;
        existing.OccurrenceOverridesJson = schedule.OccurrenceOverridesJson;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<SubscriptionSchedule>> GetSubscriptionSchedulesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyCollection<SubscriptionSchedule>>(_subscriptionSchedules);
    }

    public Task<IReadOnlyCollection<ServicePackage>> GetServicePackagesAsync(bool activeOnly, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<ServicePackage>>([]);

    public Task<ServicePackage?> GetServicePackageByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult<ServicePackage?>(null);

    public Task<ServicePackage> AddServicePackageAsync(ServicePackage package, CancellationToken cancellationToken)
        => Task.FromResult(package);

    public Task UpdateServicePackageAsync(ServicePackage package, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private readonly List<EquipmentItem> _equipmentItems = [];

    public Task<IReadOnlyCollection<EquipmentItem>> GetEquipmentItemsAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _equipmentItems.AsEnumerable();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return Task.FromResult<IReadOnlyCollection<EquipmentItem>>(query.ToList());
    }

    public Task<EquipmentItem?> GetEquipmentItemByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_equipmentItems.FirstOrDefault(x => x.Id == id));

    public Task<EquipmentItem> AddEquipmentItemAsync(EquipmentItem item, CancellationToken cancellationToken)
    {
        _equipmentItems.Add(item);
        return Task.FromResult(item);
    }

    public Task UpdateEquipmentItemAsync(EquipmentItem item, CancellationToken cancellationToken)
    {
        var existing = _equipmentItems.FirstOrDefault(x => x.Id == item.Id);
        if (existing is null)
        {
            _equipmentItems.Add(item);
            return Task.CompletedTask;
        }

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
        return Task.CompletedTask;
    }

    private readonly List<SessionEquipmentIssue> _sessionEquipmentIssues = [];

    public Task<IReadOnlyCollection<SessionEquipmentIssue>> GetSessionEquipmentIssuesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<SessionEquipmentIssue>>(_sessionEquipmentIssues.ToList());

    public Task AddSessionEquipmentIssuesAsync(IReadOnlyCollection<SessionEquipmentIssue> issues, CancellationToken cancellationToken)
    {
        _sessionEquipmentIssues.AddRange(issues);
        return Task.CompletedTask;
    }

    public Task<SessionEquipmentIssue?> GetSessionEquipmentIssueByIdAsync(Guid issueId, CancellationToken cancellationToken)
        => Task.FromResult(_sessionEquipmentIssues.FirstOrDefault(x => x.Id == issueId));

    public Task UpdateSessionEquipmentIssueAsync(SessionEquipmentIssue issue, CancellationToken cancellationToken)
    {
        var existing = _sessionEquipmentIssues.FirstOrDefault(x => x.Id == issue.Id);
        if (existing is not null)
        {
            existing.ReturnedAtUtc = issue.ReturnedAtUtc;
            existing.ReturnedByStaffId = issue.ReturnedByStaffId;
        }

        return Task.CompletedTask;
    }

    private readonly List<EquipmentSaleReceipt> _equipmentSaleReceipts = [];
    private readonly List<EquipmentSaleReceiptLine> _equipmentSaleReceiptLines = [];

    public Task<IReadOnlyCollection<EquipmentSaleReceipt>> GetEquipmentSaleReceiptsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<EquipmentSaleReceipt>>(_equipmentSaleReceipts.ToList());

    public Task<IReadOnlyCollection<EquipmentSaleReceiptLine>> GetEquipmentSaleReceiptLinesAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<EquipmentSaleReceiptLine>>(_equipmentSaleReceiptLines.ToList());

    public Task<EquipmentSaleReceipt?> GetEquipmentSaleReceiptByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_equipmentSaleReceipts.FirstOrDefault(x => x.Id == id));

    public Task AddEquipmentSaleReceiptAsync(
        EquipmentSaleReceipt receipt,
        IReadOnlyCollection<EquipmentSaleReceiptLine> lines,
        CancellationToken cancellationToken)
    {
        _equipmentSaleReceipts.Add(receipt);
        _equipmentSaleReceiptLines.AddRange(lines);
        return Task.CompletedTask;
    }

    public Task UpdateEquipmentSaleReceiptAsync(EquipmentSaleReceipt receipt, CancellationToken cancellationToken)
    {
        var idx = _equipmentSaleReceipts.FindIndex(x => x.Id == receipt.Id);
        if (idx >= 0)
        {
            _equipmentSaleReceipts[idx] = receipt;
        }

        return Task.CompletedTask;
    }

    private readonly List<StaffPosition> _staffPositions = [];
    private readonly List<AccessProfile> _accessProfiles = [];

    public Task<IReadOnlyCollection<StaffPosition>> GetStaffPositionsAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _staffPositions.AsEnumerable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Task.FromResult<IReadOnlyCollection<StaffPosition>>(query.ToList());
    }

    public Task<StaffPosition?> GetStaffPositionByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_staffPositions.FirstOrDefault(x => x.Id == id));

    public Task<StaffPosition> AddStaffPositionAsync(StaffPosition position, CancellationToken cancellationToken)
    {
        _staffPositions.Add(position);
        return Task.FromResult(position);
    }

    public Task UpdateStaffPositionAsync(StaffPosition position, CancellationToken cancellationToken)
    {
        var existing = _staffPositions.FirstOrDefault(x => x.Id == position.Id);
        if (existing is null) { _staffPositions.Add(position); return Task.CompletedTask; }
        existing.Name = position.Name;
        existing.Description = position.Description;
        existing.DefaultAccessProfileId = position.DefaultAccessProfileId;
        existing.IsActive = position.IsActive;
        existing.UpdatedAtUtc = position.UpdatedAtUtc;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<AccessProfile>> GetAccessProfilesAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _accessProfiles.AsEnumerable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Task.FromResult<IReadOnlyCollection<AccessProfile>>(query.ToList());
    }

    public Task<AccessProfile?> GetAccessProfileByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_accessProfiles.FirstOrDefault(x => x.Id == id));

    public Task<AccessProfile> AddAccessProfileAsync(AccessProfile profile, CancellationToken cancellationToken)
    {
        _accessProfiles.Add(profile);
        return Task.FromResult(profile);
    }

    public Task UpdateAccessProfileAsync(AccessProfile profile, CancellationToken cancellationToken)
    {
        var existing = _accessProfiles.FirstOrDefault(x => x.Id == profile.Id);
        if (existing is null) { _accessProfiles.Add(profile); return Task.CompletedTask; }
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
        existing.UpdatedAtUtc = profile.UpdatedAtUtc;
        return Task.CompletedTask;
    }

    private readonly List<StaffMember> _staffMembers = [];

    public Task<IReadOnlyCollection<StaffMember>> GetStaffMembersAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = _staffMembers.AsEnumerable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Task.FromResult<IReadOnlyCollection<StaffMember>>(query.ToList());
    }

    public Task<StaffMember?> GetStaffMemberByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_staffMembers.FirstOrDefault(x => x.Id == id));

    public Task<StaffMember?> GetStaffMemberByPinAsync(string pin, CancellationToken cancellationToken)
    {
        var hash = StaffPinHasher.Hash(pin);
        var member = _staffMembers.FirstOrDefault(x =>
            x.PinHash == hash && x.IsActive && !x.IsDeleted);
        return Task.FromResult(member);
    }

    public Task<StaffMember> AddStaffMemberAsync(StaffMember member, CancellationToken cancellationToken)
    {
        _staffMembers.Add(member);
        return Task.FromResult(member);
    }

    public Task UpdateStaffMemberAsync(StaffMember member, CancellationToken cancellationToken)
    {
        var existing = _staffMembers.FirstOrDefault(x => x.Id == member.Id);
        if (existing is null) { _staffMembers.Add(member); return Task.CompletedTask; }
        existing.FirstName = member.FirstName;
        existing.LastName = member.LastName;
        existing.StaffPositionId = member.StaffPositionId;
        existing.AccessProfileId = member.AccessProfileId;
        existing.PhoneNumber = member.PhoneNumber;
        existing.PinHash = member.PinHash;
        existing.IsActive = member.IsActive;
        existing.UpdatedAtUtc = member.UpdatedAtUtc;
        return Task.CompletedTask;
    }

    public Task<bool> IsStaffPinInUseAsync(string pin, Guid? excludeMemberId, CancellationToken cancellationToken)
    {
        var hash = StaffPinHasher.Hash(pin);
        var inUse = _staffMembers.Any(x =>
            x.PinHash == hash
            && (excludeMemberId is null || excludeMemberId == Guid.Empty || x.Id != excludeMemberId));
        return Task.FromResult(inUse);
    }

    public Task<bool> IsStaffPhoneInUseAsync(string phoneNumber, Guid? excludeMemberId, CancellationToken cancellationToken)
    {
        var phone = (phoneNumber ?? "").Trim();
        var inUse = _staffMembers.Any(x =>
            !string.IsNullOrWhiteSpace(x.PhoneNumber)
            && string.Equals(x.PhoneNumber.Trim(), phone, StringComparison.Ordinal)
            && (excludeMemberId is null || excludeMemberId == Guid.Empty || x.Id != excludeMemberId));
        return Task.FromResult(inUse);
    }
}
