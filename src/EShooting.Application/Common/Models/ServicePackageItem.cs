using EShooting.Domain.Enums;

namespace EShooting.Application.Common.Models;

public sealed class ServicePackageItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public PackageBillingType BillingType { get; init; }
    public PackageScope Scope { get; init; }
    public PackageSchedulingMode SchedulingMode { get; init; }
    public decimal Price { get; init; }
    public int SessionDurationMinutes { get; init; }
    public int? PeriodMinutesQuota { get; init; }
    public string? WeeklyDaysCsv { get; init; }
    public int? ValidityDays { get; init; }
    public bool UnlimitedGym { get; init; }
    public bool IsActive { get; init; }
}
