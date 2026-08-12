namespace EShooting.Web.Contracts.Athletes;

public sealed class ChangeCustomerPackageRequest
{
    public Guid NewServicePackageId { get; set; }
    public DateTime PeriodStartLocal { get; set; }
    public DateTime PeriodEndLocal { get; set; }

    /// <summary>Aylıq sabit plan üçün ay sayı (1–12). Bitmə N-ci gediş gününə görə hesablanır.</summary>
    public int? PeriodMonths { get; set; }

    public decimal DiscountAmount { get; set; }
    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }
    public bool IsComplimentary { get; set; }
    public bool ConfirmDifference { get; set; }

    /// <summary>
    /// «Sadəcə yenilə» — bitib/bitməyib baxılmır; seçilmiş paket+müddət ödənişsiz yazılır,
    /// yeni ödəniş qeydi yaranmır, köhnə ödənişlər saxlanır.
    /// </summary>
    public bool SkipPayment { get; set; }

    /// <summary>FixedWeekly üçün həftə günləri (0=Bazar … 6=Şənbə).</summary>
    public List<int>? WeeklyDaysOfWeek { get; set; }

    /// <summary>FixedWeekly üçün HH:mm.</summary>
    public string? WeeklyStartTimeLocal { get; set; }
}
