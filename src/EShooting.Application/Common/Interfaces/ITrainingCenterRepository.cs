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
    Task<IReadOnlyCollection<TrainingSession>> GetSessionsByLocalDateRangeAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        CancellationToken cancellationToken);
    Task<Lane?> GetLaneByNumberAsync(int laneNumber, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Lane>> GetLanesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Athlete>> GetAthletesAsync(CancellationToken cancellationToken);
    Task<Athlete?> GetAthleteByIdAsync(Guid athleteId, CancellationToken cancellationToken);
    Task<Athlete?> FindAthleteForLookupAsync(
        string phoneDigits,
        string emailNormalized,
        string idCardNormalized,
        CancellationToken cancellationToken,
        bool includeInactive = false);
    Task<Athlete?> FindAthleteByExactPhoneAsync(
        string phoneDigits,
        CancellationToken cancellationToken,
        bool includeInactive = false);
    Task<(Guid SessionId, int LaneNumber)?> TryGetActiveSessionForAthleteAsync(Guid athleteId, CancellationToken cancellationToken);
    Task<SubscriptionSchedule> AddSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken);
    Task UpdateSubscriptionScheduleAsync(SubscriptionSchedule schedule, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SubscriptionSchedule>> GetSubscriptionSchedulesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ServicePackage>> GetServicePackagesAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<ServicePackage?> GetServicePackageByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServicePackage> AddServicePackageAsync(ServicePackage package, CancellationToken cancellationToken);
    Task UpdateServicePackageAsync(ServicePackage package, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EquipmentItem>> GetEquipmentItemsAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<EquipmentItem?> GetEquipmentItemByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<EquipmentItem> AddEquipmentItemAsync(EquipmentItem item, CancellationToken cancellationToken);
    Task UpdateEquipmentItemAsync(EquipmentItem item, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SessionEquipmentIssue>> GetSessionEquipmentIssuesAsync(CancellationToken cancellationToken);
    Task AddSessionEquipmentIssuesAsync(IReadOnlyCollection<SessionEquipmentIssue> issues, CancellationToken cancellationToken);
    Task<SessionEquipmentIssue?> GetSessionEquipmentIssueByIdAsync(Guid issueId, CancellationToken cancellationToken);
    Task UpdateSessionEquipmentIssueAsync(SessionEquipmentIssue issue, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EquipmentSaleReceipt>> GetEquipmentSaleReceiptsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EquipmentSaleReceiptLine>> GetEquipmentSaleReceiptLinesAsync(CancellationToken cancellationToken);
    Task<EquipmentSaleReceipt?> GetEquipmentSaleReceiptByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddEquipmentSaleReceiptAsync(EquipmentSaleReceipt receipt, IReadOnlyCollection<EquipmentSaleReceiptLine> lines, CancellationToken cancellationToken);
    Task CreateEquipmentSaleAsync(
        EquipmentSaleReceipt receipt,
        IReadOnlyCollection<EquipmentSaleReceiptLine> lines,
        IReadOnlyDictionary<Guid, int> quantitySoldByItemId,
        CancellationToken cancellationToken);
    Task UpdateEquipmentSaleReceiptAsync(EquipmentSaleReceipt receipt, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StaffPosition>> GetStaffPositionsAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<StaffPosition?> GetStaffPositionByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<StaffPosition> AddStaffPositionAsync(StaffPosition position, CancellationToken cancellationToken);
    Task UpdateStaffPositionAsync(StaffPosition position, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AccessProfile>> GetAccessProfilesAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<AccessProfile?> GetAccessProfileByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AccessProfile> AddAccessProfileAsync(AccessProfile profile, CancellationToken cancellationToken);
    Task UpdateAccessProfileAsync(AccessProfile profile, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StaffMember>> GetStaffMembersAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<StaffMember?> GetStaffMemberByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<StaffMember?> GetStaffMemberByPinAsync(string pin, CancellationToken cancellationToken);
    Task<StaffMember> AddStaffMemberAsync(StaffMember member, CancellationToken cancellationToken);
    Task UpdateStaffMemberAsync(StaffMember member, CancellationToken cancellationToken);
    Task<bool> IsStaffPinInUseAsync(string pin, Guid? excludeMemberId, CancellationToken cancellationToken);
    Task<bool> IsStaffPhoneInUseAsync(string phoneNumber, Guid? excludeMemberId, CancellationToken cancellationToken);

    Task<CustomerPackageRecord> AddCustomerPackageRecordAsync(CustomerPackageRecord record, CancellationToken cancellationToken);
    Task<CustomerPackageRecord?> GetCustomerPackageRecordByIdAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateCustomerPackageRecordAsync(CustomerPackageRecord record, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CustomerPackageRecord>> GetCustomerPackageRecordsAsync(CancellationToken cancellationToken);
}
