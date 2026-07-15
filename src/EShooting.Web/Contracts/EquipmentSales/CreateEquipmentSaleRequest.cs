namespace EShooting.Web.Contracts.EquipmentSales;

public sealed class CreateEquipmentSaleRequest
{
    public Guid AthleteId { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }

    /// <summary>Satış zamanı müştəriyə çek verilib.</summary>
    public bool ReceiptIssued { get; set; }

    public IReadOnlyList<EquipmentSaleLineRequest> Lines { get; set; } = [];
}
