namespace EShooting.Domain.Entities;

/// <summary>Müştərinin paket üzrə ödəniş öhdəliyi (zolağa yazma / abunə zamanı).</summary>
public sealed class CustomerPackageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AthleteId { get; set; }
    public Guid? ServicePackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string BillingTypeLabel { get; set; } = string.Empty;
    public decimal PriceDue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }
    public decimal AmountPaid { get; set; }
    public bool IsComplimentary { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? SubscriptionScheduleId { get; set; }
    public Guid? CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public decimal AmountPayable => Math.Max(0m, PriceDue - DiscountAmount);
    public decimal AmountRemaining => Math.Max(0m, AmountPayable - AmountPaid);
}
