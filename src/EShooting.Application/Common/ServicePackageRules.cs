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
        if (scope == PackageScope.Vip || billingType == PackageBillingType.Vip)
        {
            return true;
        }

        return schedulingMode == PackageSchedulingMode.WalkInFlexible && sessionDurationMinutes == 0;
    }
}
