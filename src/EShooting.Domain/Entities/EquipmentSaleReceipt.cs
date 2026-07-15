using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

public sealed class EquipmentSaleReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Müştəri (avadanlıq satışı bu şəxsə bağlanır).</summary>
    public Guid AthleteId { get; set; }

    /// <summary>Satış / qaytarma.</summary>
    public EquipmentSaleReceiptType Type { get; set; } = EquipmentSaleReceiptType.Sale;

    /// <summary>Qaytarma üçün: hansı satış qaiməsinə bağlıdır.</summary>
    public Guid? OriginalReceiptId { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }

    public Guid? CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Satış zamanı müştəriyə çek verilib.</summary>
    public bool ReceiptIssued { get; set; }

    public decimal AmountPaid => AmountPaidCash + AmountPaidCard;
    public decimal AmountPayable => Math.Max(0m, TotalAmount - DiscountAmount);
    public decimal AmountRemaining => Math.Max(0m, AmountPayable - AmountPaid);
}

