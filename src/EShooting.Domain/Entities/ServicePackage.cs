using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

/// <summary>Admin kataloqu — resepsiyada satılan paket şablonu.</summary>
public sealed class ServicePackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public PackageBillingType BillingType { get; set; }
    public PackageScope Scope { get; set; }
    public PackageSchedulingMode SchedulingMode { get; set; }
    public decimal Price { get; set; }

    /// <summary>Hər sessiyanın default müddəti (dəqiqə).</summary>
    public int SessionDurationMinutes { get; set; }

    /// <summary>Sabit planda dövr ərzində cəmi dəqiqə (məs. aylıq 720 = 12 saat).</summary>
    public int? PeriodMinutesQuota { get; set; }

    /// <summary>Həftə günləri (0=Bazar … 6=Şənbə), məs: "1,3,5".</summary>
    public string? WeeklyDaysCsv { get; set; }

    /// <summary>Abunə müddəti (gün): aylıq 30, illik 365.</summary>
    public int? ValidityDays { get; set; }

    public bool UnlimitedGym { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
