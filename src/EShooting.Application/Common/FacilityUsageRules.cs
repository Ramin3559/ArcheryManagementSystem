using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

public static class FacilityUsageRules
{
    public static bool PackageRequiresVisitChoice(PackageScope scope)
        => scope == PackageScope.Full;

    public static FacilityUsage InferFromLane(int laneNumber)
        => GymLaneRules.IsGymLane(laneNumber) ? FacilityUsage.Gym : FacilityUsage.Archery;

    public static FacilityUsage Resolve(FacilityUsage? stored, int laneNumber)
        => stored ?? InferFromLane(laneNumber);

    public static string Label(FacilityUsage usage) => usage switch
    {
        FacilityUsage.Gym => "Trenajor",
        FacilityUsage.Both => "Hər ikisi",
        _ => "Oxatma"
    };

    public static string FormatVisitPlace(int? laneNumber, FacilityUsage? stored)
    {
        var lane = laneNumber ?? 0;
        var usage = Resolve(stored, lane);
        if (usage == FacilityUsage.Gym)
        {
            return "Trenajor";
        }

        if (usage == FacilityUsage.Both && GymLaneRules.IsGymLane(lane))
        {
            return "Trenajor · Hər ikisi";
        }

        var lanePart = lane > 0 && !GymLaneRules.IsGymLane(lane) ? $"Zolaq {lane} · " : "";
        return lanePart + Label(usage);
    }

    public static bool HasEnabledSubscription(
        Guid athleteId,
        IEnumerable<SubscriptionSchedule>? schedules)
        => schedules?.Any(s => s.AthleteId == athleteId && s.IsEnabled) == true;

    public static bool IsOneTimeBillingLabel(string? label)
    {
        var t = (label ?? "").Trim();
        if (t.Length == 0)
        {
            return false;
        }

        return t.Equals("OneTime", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Birdəfəlik", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Birdefəlik", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Birdefelik", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsOneTimeBilling(
        CustomerPackageRecord record,
        IEnumerable<ServicePackage>? packages = null)
    {
        if (IsOneTimeBillingLabel(record.BillingTypeLabel))
        {
            return true;
        }

        if (record.ServicePackageId is Guid pkgId && packages is not null)
        {
            var pkg = packages.FirstOrDefault(p => p.Id == pkgId);
            if (pkg is not null)
            {
                return pkg.BillingType == PackageBillingType.OneTime;
            }
        }

        return false;
    }

    public static CustomerPackageRecord? ResolveCurrentPackageRecord(
        Guid athleteId,
        IEnumerable<CustomerPackageRecord> records,
        bool hasEnabledSubscription,
        IEnumerable<ServicePackage>? packages = null)
    {
        var mine = records.Where(r => r.AthleteId == athleteId).ToList();
        if (mine.Count == 0)
        {
            return null;
        }

        var active = mine
            .Where(r => r.IsActive && r.ServicePackageId is Guid)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault()
            ?? mine.Where(r => r.IsActive).OrderByDescending(r => r.CreatedAtUtc).FirstOrDefault();
        if (active is not null)
        {
            return active;
        }

        if (hasEnabledSubscription)
        {
            var subscriptionRecord = mine
                .Where(r => r.ServicePackageId is Guid && !IsOneTimeBilling(r, packages))
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault()
                ?? mine
                    .Where(r => !IsOneTimeBilling(r, packages))
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .FirstOrDefault();
            if (subscriptionRecord is not null)
            {
                return subscriptionRecord;
            }
        }

        return mine
            .Where(r => r.ServicePackageId is Guid)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault()
            ?? mine.OrderByDescending(r => r.CreatedAtUtc).FirstOrDefault();
    }

    public static PackageScope? CurrentPackageScope(
        Guid athleteId,
        IEnumerable<CustomerPackageRecord> records,
        IEnumerable<ServicePackage> packages,
        IEnumerable<SubscriptionSchedule>? schedules = null)
    {
        var rec = ResolveCurrentPackageRecord(
            athleteId,
            records,
            HasEnabledSubscription(athleteId, schedules),
            packages);
        if (rec?.ServicePackageId is not Guid pkgId)
        {
            return null;
        }

        return packages.FirstOrDefault(p => p.Id == pkgId)?.Scope;
    }

    public static string? CurrentPackageName(
        Guid athleteId,
        IEnumerable<CustomerPackageRecord> records,
        IEnumerable<ServicePackage> packages,
        IEnumerable<SubscriptionSchedule>? schedules = null)
    {
        var rec = ResolveCurrentPackageRecord(
            athleteId,
            records,
            HasEnabledSubscription(athleteId, schedules),
            packages);
        if (rec is null)
        {
            return null;
        }

        if (rec.ServicePackageId is Guid pkgId)
        {
            var pkgName = packages.FirstOrDefault(p => p.Id == pkgId)?.Name;
            if (!string.IsNullOrWhiteSpace(pkgName))
            {
                return pkgName.Trim();
            }
        }

        var fromRecord = (rec.PackageName ?? "").Trim();
        return string.IsNullOrWhiteSpace(fromRecord) ? null : fromRecord;
    }

    public static string ScopeLabel(PackageScope? scope) => scope switch
    {
        PackageScope.Full => "Hər ikisi",
        PackageScope.Gym => "Yalnız Trenajor",
        PackageScope.Archery => "Yalnız oxatma",
        PackageScope.Vip => "VIP",
        _ => ""
    };

    public static FacilityUsage ResolveForWrite(
        FacilityUsage? requested,
        int laneNumber,
        PackageScope? packageScope)
    {
        if (packageScope is PackageScope scope && PackageRequiresVisitChoice(scope))
        {
            if (requested is not FacilityUsage chosen)
            {
                throw new InvalidOperationException("İstifadə sahəsini seçin (yalnız oxatma, yalnız trenajor və ya hər ikisi).");
            }

            if (chosen is not (FacilityUsage.Archery or FacilityUsage.Gym or FacilityUsage.Both))
            {
                throw new InvalidOperationException("İstifadə sahəsini seçin (yalnız oxatma, yalnız trenajor və ya hər ikisi).");
            }

            return chosen;
        }

        if (packageScope == PackageScope.Gym || GymLaneRules.IsGymLane(laneNumber))
        {
            return FacilityUsage.Gym;
        }

        return requested == FacilityUsage.Gym ? FacilityUsage.Gym : FacilityUsage.Archery;
    }

    public static int ResolveLaneNumber(FacilityUsage usage, int requestedLaneNumber)
    {
        if (usage == FacilityUsage.Gym)
        {
            return GymLaneRules.LaneNumber;
        }

        return requestedLaneNumber;
    }
}
