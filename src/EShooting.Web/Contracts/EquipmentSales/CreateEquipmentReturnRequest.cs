namespace EShooting.Web.Contracts.EquipmentSales;

public sealed class CreateEquipmentReturnRequest
{
    public Guid AthleteId { get; set; }
    public Guid OriginalReceiptId { get; set; }

    /// <summary>Satışda çek verilmişdisə, qaytarmada çek təqdim olunub.</summary>
    public bool ReceiptPresented { get; set; }

    /// <summary>Geri ödəniş — nağd.</summary>
    public decimal AmountPaidCash { get; set; }

    /// <summary>Geri ödəniş — kart.</summary>
    public decimal AmountPaidCard { get; set; }

    public IReadOnlyList<EquipmentSaleLineRequest> Lines { get; set; } = [];
}
