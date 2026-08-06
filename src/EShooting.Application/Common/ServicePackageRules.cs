using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Common;

public static class ServicePackageRules
{
    public static bool IsVipPackage(ServicePackage package) =>
        IsVipPackage(package.BillingType, package.Scope, package.SchedulingMode, package.SessionDurationMinutes);

    public static bool IsVipPackage(
        PackageBillingType billingType,
        PackageScope scope,
        PackageSchedulingMode schedulingMode,
        int sessionDurationMinutes)
    {
        // VIP yalnız paket növü/scope ilə — müddətsiz Limitsiz VIP sayılmır.
        _ = schedulingMode;
        _ = sessionDurationMinutes;
        return scope == PackageScope.Vip || billingType == PackageBillingType.Vip;
    }

    public static bool IsUnlimitedPackage(ServicePackage package) =>
        IsUnlimitedPackage(package.BillingType, package.SchedulingMode, package.SessionDurationMinutes, package.Scope);

    public static bool IsUnlimitedPackage(
        PackageBillingType billingType,
        PackageSchedulingMode schedulingMode,
        int sessionDurationMinutes)
        => IsUnlimitedPackage(billingType, schedulingMode, sessionDurationMinutes, PackageScope.Archery);

    public static bool IsUnlimitedPackage(
        PackageBillingType billingType,
        PackageSchedulingMode schedulingMode,
        int sessionDurationMinutes,
        PackageScope scope)
    {
        if (IsVipPackage(billingType, scope, schedulingMode, sessionDurationMinutes))
        {
            return false;
        }

        if (billingType == PackageBillingType.Unlimited)
        {
            return true;
        }

        // Köhnə walk-in full paketlər (vaxtlı və ya müddətsiz).
        _ = sessionDurationMinutes;
        return schedulingMode == PackageSchedulingMode.WalkInFlexible;
    }
}
