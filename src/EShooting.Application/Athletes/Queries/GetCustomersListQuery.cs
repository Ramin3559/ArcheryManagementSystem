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

        IEnumerable<Athlete> query = athletes;
        if (!request.IncludeGroupPlaceholders)
        {
            query = query.Where(AthleteSearchRules.IsSearchable);
        }
        else if (!request.IncludeInactive)
        {
            query = query.Where(x => x.IsActive && !x.IsGroupPlaceholder);
        }

        if (!request.IncludeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var search = (request.Search ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => MatchesCustomerSearch(a, search, staffNameById, packageRecords));
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

        if (string.Equals(request.Active, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }
        else if (string.Equals(request.Active, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
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

            if (!MatchesCustomerTypeFilter(request.CustomerType, hasLane, hasStandaloneSale))
            {
                continue;
            }

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
            var registeredLocalDate = registeredLocal.Date;
            if (request.RegisteredFrom is DateTime from && registeredLocalDate < from.Date)
            {
                continue;
            }

            if (request.RegisteredTo is DateTime to && registeredLocalDate > to.Date)
            {
                continue;
            }

            var records = packageRecords.Where(r => r.AthleteId == athlete.Id && r.IsActive && !r.IsComplimentary).ToList();

            var lastSession = athleteSessions
                .OrderByDescending(s => s.StartTimeUtc)
                .FirstOrDefault();
            string? lastLaneVisit = null;
            int? lastLaneNumber = null;
            if (lastSession is not null)
            {
                lastLaneVisit = AzerbaijanTime.UtcToLocalDateTime(
                        DateTimeAssumedUtc.AsUtc(lastSession.StartTimeUtc))
                    .ToString("yyyy-MM-dd HH:mm");
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
            var staffName = ResolveRegisteredByStaffName(
                athlete.RegisteredByStaffId,
                records,
                staffNameById);

            items.Add(new CustomerListItem
            {
                Id = athlete.Id,
                FullName = athlete.FullName,
                PhoneNumber = athlete.PhoneNumber,
                Email = athlete.Email,
                IdCardNumber = athlete.IdCardNumber,
                ClubCardNumber = athlete.ClubCardNumber,
                Category = athlete.Category,
                CategoryLabel = CategoryLabel(athlete.Category),
                IsVip = athlete.IsVip,
                IsActive = athlete.IsActive,
                IsSubscriber = athlete.IsSubscriber,
                PackageTypeLabel = packageType,
                SubscriptionFromLocal = activeSub?.ActiveFromDateLocal.ToString("yyyy-MM-dd"),
                SubscriptionToLocal = activeSub?.ActiveToDateLocal.ToString("yyyy-MM-dd"),
                RegisteredAtLocal = registeredLocal.ToString("yyyy-MM-dd HH:mm"),
                RegisteredByStaffName = staffName,
                HasLaneHistory = hasLane,
                HasStandaloneEquipmentPurchase = hasStandaloneSale,
                CustomerTypeLabel = ResolveCustomerTypeLabel(hasLane, hasStandaloneSale),
                HasSessionEquipmentRental = hasSessionRental,
                HasPendingSessionRental = hasPendingRental,
                HasEquipmentHistory = hasSessionRental,
                HasPendingEquipment = hasPendingRental,
                LastLaneVisitLocal = lastLaneVisit,
                LastLaneNumber = lastLaneNumber,
                LastVisitLocal = lastLaneVisit,
                ActiveLaneNumber = activeLane,
                CurrentPackageName = latestRecord?.PackageName ?? (activeSub is not null ? "Abunə" : null)
            });
        }

        return new CustomersListResult
        {
            Items = items,
            TotalCount = items.Count
        };
    }

    private static bool MatchesCustomerSearch(
        Athlete athlete,
        string search,
        IReadOnlyDictionary<Guid, string> staffNameById,
        IReadOnlyList<CustomerPackageRecord> packageRecords)
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

        if (qDigits.Length > 0 && (athlete.PhoneNumber ?? "").Contains(qDigits))
        {
            return true;
        }

        if (athlete.RegisteredByStaffId is Guid registeredStaffId
            && staffNameById.TryGetValue(registeredStaffId, out var registeredBy)
            && registeredBy.ToLowerInvariant().Contains(qLower))
        {
            return true;
        }

        return packageRecords
            .Where(r => r.AthleteId == athlete.Id && r.CreatedByStaffId is Guid staffId && staffId != Guid.Empty)
            .Select(r => staffNameById.GetValueOrDefault(r.CreatedByStaffId!.Value, ""))
            .Any(name => name.ToLowerInvariant().Contains(qLower));
    }

    private static bool ContainsIgnoreCase(string? value, string needleLower) =>
        (value ?? "").ToLowerInvariant().Contains(needleLower);

    private static string ResolvePackageType(Athlete athlete, SubscriptionSchedule? activeSub)
    {
        if (activeSub is not null && activeSub.IsFullPackage && activeSub.DurationMinutes == 0)
        {
            return "VIP abunə";
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
            "subscription" or "abune" => packageType == "Abunə",
            "vip" => packageType == "VIP abunə",
            _ => true
        };
    }

    private static bool MatchesCustomerTypeFilter(string? filter, bool hasLane, bool hasStandaloneSale)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return filter.ToLowerInvariant() switch
        {
            "lane" or "zolaq" => hasLane && !hasStandaloneSale,
            "buyer" or "alici" or "avadanliq" => hasStandaloneSale && !hasLane,
            "both" or "her-ikisi" => hasLane && hasStandaloneSale,
            "none" or "hec-biri" => !hasLane && !hasStandaloneSale,
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

    private static string ResolveCustomerTypeLabel(bool hasLane, bool hasStandaloneSale)
    {
        if (hasLane && hasStandaloneSale)
        {
            return "Alıcı müştəri";
        }

        if (hasLane)
        {
            return "Müştəri";
        }

        if (hasStandaloneSale)
        {
            return "Alıcı";
        }

        return "—";
    }

    private static string CategoryLabel(CustomerCategory category) => category switch
    {
        CustomerCategory.Amateur => "Həvəskar",
        CustomerCategory.Professional => "Peşəkar",
        CustomerCategory.Coach => "Məşqçi",
        _ => category.ToString()
    };

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
}
