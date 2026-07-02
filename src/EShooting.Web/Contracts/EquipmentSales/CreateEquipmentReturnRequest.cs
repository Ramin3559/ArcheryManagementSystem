namespace EShooting.Web.Contracts.EquipmentSales;

public sealed class CreateEquipmentReturnRequest
{
    public Guid AthleteId { get; set; }
    public Guid OriginalReceiptId { get; set; }
    public IReadOnlyList<EquipmentSaleLineRequest> Lines { get; set; } = [];
}

