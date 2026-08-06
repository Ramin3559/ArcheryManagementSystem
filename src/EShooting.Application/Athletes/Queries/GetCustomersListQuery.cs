using EShooting.Application.Athletes;
using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Athletes.Queries;

public sealed record GetCustomersListQuery(
    string? Search = null,
    string? Vip = null,
    string? PackageType = null,
    string? CustomerType = null,
    string? SessionRental = null,
    string? Active = null,
    CustomerCategory? Category = null,
    DateTime? RegisteredFrom = null,
    DateTime? RegisteredTo = null,
    bool IncludeInactive = false,
    bool IncludeGroupPlaceholders = false) : IRequest<CustomersListResult>;

public sealed class GetCustomersListQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetCustomersListQuery, CustomersListResult>
{
    public async Task<CustomersListResult> Handle(GetCustomersListQuery request, CancellationToken cancellationToken)
    {
        var athletes = (await repository.GetAthletesAsync(cancellationToken)).ToList();
        var schedules = (await repository.GetSubscriptionSchedulesAsync(cancellationToken)).ToList();
        var sessions = (await repository.GetSessionsLightAsync(cancellationToken)).ToList();
        var issues = (await repository.GetSessionEquipmentIssuesAsync(cancellationToken)).ToList();
        var equipment = (await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken)).ToList();
        var packageRecords = (await repository.GetCustomerPackageRecordsAsync(cancellationToken)).ToList();
        var equipmentReceipts = (await repository.GetEquipmentSaleReceiptsAsync(cancellationToken)).ToList();
        var staff = (await repository.GetStaffMembersAsync(activeOnly: false, cancellationToken)).ToList();
        var lanes = (await repository.GetLanesAsync(cancellationToken)).ToList();
        var nowUtc = DateTime.UtcNow;
        var todayLocal = DateTime.Now.Date;

        var staffNameById = staff.ToDictionary(
            x => x.Id,
            x => $"{x.FirstName} {x.LastName}".Trim());

        var laneById = lanes.ToDictionary(x => x.Id, x => x.Number);

        IEnumerable<Athlete> query = athletes.Where(a => !AthleteSearchRules.IsGroupSessionPlaceholder(a));

        var activeKey = (request.Active ?? "").Trim().ToLowerInvariant();
        if (activeKey is "inactive" or "deleted" or "silinmis")
        {
            query = query.Where(x => !x.IsActive);
        }
        else if (activeKey is "active" or "aktiv")
        {
            query = query.Where(x => x.IsActive);
        }
        else if (!request.IncludeInactive)
        {
            // Hamısı + includeInactive=false → yalnız aktiv (köhnə davranış).
            query = query.Where(x => x.IsActive);
        }

        var search = (request.Search ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => MatchesCustomerSearch(a, search));
        }

        if (request.Category is not null)
        {
            query = query.Where(x => x.Category == request.Category);
        }

        if (string.Equals(request.Vip, "yes", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsVip);
        }
        else if (string.Equals(request.Vip, "no", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsVip);
        }

        var items = new List<CustomerListItem>();
        foreach (var athlete in query.OrderBy(x => x.FullName))
        {
            var athleteSchedules = schedules.Where(s => s.AthleteId == athlete.Id && s.IsEnabled).ToList();
            var activeSub = athleteSchedules
                .Where(s => s.ActiveFromDateLocal.Date <= todayLocal && s.ActiveToDateLocal.Date >= todayLocal)
                .OrderByDescending(s => s.ActiveToDateLocal)
                .FirstOrDefault();

            var packageType = ResolvePackageType(athlete, activeSub);
            if (!MatchesPackageTypeFilter(request.PackageType, packageType))
            {
                continue;
            }

            var athleteSessions = sessions.Where(s => s.AthleteId == athlete.Id).ToList();
            var hasLane = athleteSessions.Count > 0;
            var hasStandaloneSale = equipmentReceipts.Any(r =>
                r.AthleteId == athlete.Id && r.Type == EquipmentSaleReceiptType.Sale);
            var hasSessionRental = issues.Any(i => athleteSessions.Any(s => s.Id == i.SessionId));
            var hasPendingRental = issues.Any(i =>
                athleteSessions.Any(s => s.Id == i.SessionId)
                && i.IssueType == EquipmentIssueType.Rental
                && i.ReturnedAtUtc is null);

            if (!MatchesSessionRentalFilter(request.SessionRental, hasSessionRental, hasPendingRental))
            {
                continue;
            }

            var athleteRecords = packageRecords.Where(r => r.AthleteId == athlete.Id).ToList();
            var registeredUtc = AthleteRegistrationDateRules.ResolveRegisteredAtUtc(
                athlete,
                athleteSessions,
                schedules.Where(s => s.AthleteId == athlete.Id).ToList(),
                athleteRecords);
            var registeredLocal = AzerbaijanTime.UtcToLocalDateTime(registeredUtc);

            var lastSession = athleteSessions
                .OrderByDescending(s => s.StartTimeUtc)
                .FirstOrDefault();
            DateTime? lastLaneLocalDate = lastSession is null
                ? null
                : AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(lastSession.StartTimeUtc));

            // Axtarış dolu olanda tarix filtri tətbiq olunmur — bütün müştərilər arasında axtarılır.
            // Tarix filtri: son zolağa yazılma tarixinə görə.
            var hasSearch = !string.IsNullOrWhiteSpace(request.Search);
            if (!hasSearch && (request.RegisteredFrom is not null || request.RegisteredTo is not null))
            {
                if (lastLaneLocalDate is null)
                {
                    continue;
                }

                if (request.RegisteredFrom is DateTime from)
                {
                    var fromDate = DateTime.SpecifyKind(from.Date, DateTimeKind.Unspecified);
                    if (lastLaneLocalDate.Value < fromDate)
                    {
                        continue;
                    }
                }

                if (request.RegisteredTo is DateTime to)
                {
                    var toDate = DateTime.SpecifyKind(to.Date, DateTimeKind.Unspecified);
                    if (lastLaneLocalDate.Value > toDate)
                    {
                        continue;
                    }
                }
            }

            var allActiveRecords = packageRecords
                .Where(r => r.AthleteId == athlete.Id && r.IsActive)
                .ToList();
            var records = allActiveRecords.Where(r => !r.IsComplimentary).ToList();

            string? lastLaneVisit = null;
            int? lastLaneNumber = null;
            if (lastSession is not null)
            {
                lastLaneVisit = DateDisplayFormats.FormatDateTime(
                    AzerbaijanTime.UtcToLocalDateTime(
                        DateTimeAssumedUtc.AsUtc(lastSession.StartTimeUtc)));
                if (laneById.TryGetValue(lastSession.LaneId, out var lastLn))
                {
                    lastLaneNumber = lastLn;
                }
            }

            var activeSession = athleteSessions
                .FirstOrDefault(s => SessionHousekeeping.IsAthleteSessionCurrentlyActive(s, nowUtc));
            int? activeLane = null;
            if (activeSession is not null && laneById.TryGetValue(activeSession.LaneId, out var ln))
            {
                activeLane = ln;
            }

            var latestRecord = records.OrderByDescending(r => r.CreatedAtUtc).FirstOrDefault();
            var latestBilling = allActiveRecords.OrderByDescending(r => r.CreatedAtUtc).FirstOrDefault();
            var paymentLabel = "—";
            decimal? paid = null;
            decimal? paidCash = null;
            decimal? paidCard = null;
            var complimentary = false;
            if (latestBilling is not null)
            {
                complimentary = latestBilling.IsComplimentary;
                paid = latestBilling.AmountPaid;
                paidCash = latestBilling.AmountPaidCash;
                paidCard = latestBilling.AmountPaidCard;
                if (complimentary)
                {
                    paymentLabel = "Pulsuz";
                }
                else if (latestBilling.AmountPaidCash > 0m && latestBilling.AmountPaidCard > 0m)
                {
                    paymentLabel =
                        $"{latestBilling.AmountPaid:0.##} AZN (nağd {latestBilling.AmountPaidCash:0.##} + kart {latestBilling.AmountPaidCard:0.##})";
                }
                else if (latestBilling.AmountPaidCard > 0m)
                {
                    paymentLabel = $"{latestBilling.AmountPaid:0.##} AZN (kart)";
                }
                else if (latestBilling.AmountPaidCash > 0m)
                {
                    paymentLabel = $"{latestBilling.AmountPaid:0.##} AZN (nağd)";
                }
                else
                {
                    paymentLabel = $"{latestBilling.AmountPaid:0.##} AZN";
                }
            }

            var staffName = ResolveRegisteredByStaffName(
                athlete.RegisteredByStaffId,
                allActiveRecords,
                staffNameById);
            var deletedByName = ResolveDeletedByName(
                athlete,
                staffNameById);
            string? deletedAtLocal = null;
            if (athlete.DeletedAtUtc is DateTime deletedUtc)
            {
                deletedAtLocal = DateDisplayFormats.FormatDateTime(
                    AzerbaijanTime.UtcToLocalDateTime(DateTimeAssumedUtc.AsUtc(deletedUtc)));
            }

            items.Add(new CustomerListItem
            {
                Id = athlete.Id,
                FullName = athlete.FullName,
                PhoneNumber = athlete.PhoneNumber,
                Email = athlete.Email,
                IdCardNumber = athlete.IdCardNumber,
                ClubCardNumber = athlete.ClubCardNumber,
                ClubCardLabel = FormatClubCardLabel(athlete),
                HasClubCard = !string.IsNullOrWhiteSpace(athlete.ClubCardNumber),
                Category = athlete.Category,
                CategoryLabel = CategoryLabel(athlete.Category),
                IsVip = athlete.IsVip,
                IsActive = athlete.IsActive,
                IsSubscriber = athlete.IsSubscriber,
                PackageTypeLabel = packageType,
                SubscriptionFromLocal = activeSub is null ? null : DateDisplayFormats.FormatDate(activeSub.ActiveFromDateLocal),
                SubscriptionToLocal = activeSub is null ? null : DateDisplayFormats.FormatDate(activeSub.ActiveToDateLocal),
                RegisteredAtLocal = DateDisplayFormats.FormatDateTime(registeredLocal),
                RegisteredByStaffName = staffName,
                DeletedAtLocal = deletedAtLocal,
                DeletedByName = deletedByName,
                HasLaneHistory = hasLane,
                HasStandaloneEquipmentPurchase = hasStandaloneSale,
                CustomerTypeLabel = "Müştəri",
                HasSessionEquipmentRental = hasSessionRental,
                HasPendingSessionRental = hasPendingRental,
                HasEquipmentHistory = hasSessionRental,
                HasPendingEquipment = hasPendingRental,
                LastLaneVisitLocal = lastLaneVisit,
                LastLaneNumber = lastLaneNumber,
                LastVisitLocal = lastLaneVisit,
                ActiveLaneNumber = activeLane,
                CurrentPackageName = latestRecord?.PackageName ?? (activeSub is not null ? "Abunə" : null),
                LatestAmountPaid = paid,
                LatestAmountPaidCash = paidCash,
                LatestAmountPaidCard = paidCard,
                LatestPaymentIsComplimentary = complimentary,
                LatestPaymentLabel = paymentLabel,
                LatestPackageRecordId = latestBilling?.Id,
                HasPackagePayments = allActiveRecords.Count > 0
            });
        }

        return new CustomersListResult
        {
            Items = items,
            TotalCount = items.Count
        };
    }

    private static bool MatchesCustomerSearch(Athlete athlete, string search)
    {
        var qLower = search.ToLowerInvariant();
        var qDigits = new string(search.Where(char.IsDigit).ToArray());

        if (ContainsIgnoreCase(athlete.FullName, qLower)
            || ContainsIgnoreCase(athlete.FirstName, qLower)
            || ContainsIgnoreCase(athlete.LastName, qLower)
            || ContainsIgnoreCase(athlete.Email, qLower)
            || ContainsIgnoreCase(athlete.IdCardNumber, qLower)
            || ContainsIgnoreCase(athlete.ClubCardNumber, qLower))
        {
            return true;
        }

        return qDigits.Length > 0 && (athlete.PhoneNumber ?? "").Contains(qDigits);
    }

    private static bool ContainsIgnoreCase(string? value, string needleLower) =>
        (value ?? "").ToLowerInvariant().Contains(needleLower);

    private static string ResolvePackageType(Athlete athlete, SubscriptionSchedule? activeSub)
    {
        if (activeSub is not null && activeSub.IsFullPackage && activeSub.DurationMinutes == 0)
        {
            return athlete.IsVip ? "VIP abunə" : "Limitsiz müddətsiz";
        }

        if (athlete.IsSubscriber || activeSub is not null)
        {
            return "Abunə";
        }

        return "Birdefəlik";
    }

    private static bool MatchesPackageTypeFilter(string? filter, string packageType)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return filter.ToLowerInvariant() switch
        {
            "onetime" or "birdefelik" => packageType == "Birdefəlik",
            "subscription" or "abune" => packageType is "Abunə" or "Limitsiz müddətsiz",
            "vip" => packageType == "VIP abunə",
            _ => true
        };
    }

    private static bool MatchesSessionRentalFilter(string? filter, bool hasSessionRental, bool hasPendingRental)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return filter.ToLowerInvariant() switch
        {
            "any" or "yes" => hasSessionRental,
            "pending" => hasPendingRental,
            "none" or "no" => !hasSessionRental,
            _ => true
        };
    }

    private static string CategoryLabel(CustomerCategory category) => category switch
    {
        CustomerCategory.Amateur => "Həvəskar",
        CustomerCategory.Professional => "Peşəkar",
        CustomerCategory.Coach => "Məşqçi",
        _ => category.ToString()
    };

    private static string? FormatClubCardLabel(Athlete athlete)
    {
        var number = (athlete.ClubCardNumber ?? "").Trim();
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        return athlete.ClubCardType is ClubCardType type
            ? ClubCardTypeLabels.FormatCard(type, number)
            : number;
    }

    private static string ResolveRegisteredByStaffName(
        Guid? registeredByStaffId,
        IReadOnlyList<CustomerPackageRecord> records,
        IReadOnlyDictionary<Guid, string> staffNameById)
    {
        if (registeredByStaffId is Guid sid && staffNameById.TryGetValue(sid, out var direct))
        {
            return direct;
        }

        var fallbackStaffId = records
            .OrderBy(r => r.CreatedAtUtc)
            .Select(r => r.CreatedByStaffId)
            .FirstOrDefault(id => id is Guid g && g != Guid.Empty);

        if (fallbackStaffId is Guid fid && staffNameById.TryGetValue(fid, out var fallback))
        {
            return fallback;
        }

        return "—";
    }

    private static string ResolveDeletedByName(
        Athlete athlete,
        IReadOnlyDictionary<Guid, string> staffNameById)
    {
        if (athlete.IsActive)
        {
            return "—";
        }

        if (athlete.DeletedByStaffId is Guid sid && staffNameById.TryGetValue(sid, out var staff))
        {
            return staff;
        }

        if (!string.IsNullOrWhiteSpace(athlete.DeletedByAdminUserName))
        {
            return "Admin: " + athlete.DeletedByAdminUserName.Trim();
        }

        return "—";
    }
}
