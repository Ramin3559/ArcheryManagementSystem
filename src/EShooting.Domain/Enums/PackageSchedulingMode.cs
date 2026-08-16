namespace EShooting.Domain.Enums;

public enum PackageSchedulingMode
{
    /// <summary>Birdefəlik sessiya — sabit həftəlik plan tələb olunmur.</summary>
    None = 0,

    /// <summary>Aylıq/illik sabit həftəlik cədvəl (həftə günləri resepsiyada).</summary>
    FixedWeekly = 1,

    /// <summary>Full / çevik — gələndə boş zolaq, vaxt planı yoxdur.</summary>
    WalkInFlexible = 2,

    /// <summary>Aylıq sərbəst — həftə günü yox, dövr ərzində gediş limiti.</summary>
    FlexibleMonthly = 3
}
