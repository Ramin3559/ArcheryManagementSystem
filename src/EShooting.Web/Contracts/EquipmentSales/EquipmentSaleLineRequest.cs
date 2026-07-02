namespace EShooting.Web.Contracts.EquipmentSales;

public sealed class EquipmentSaleLineRequest
{
    public Guid EquipmentItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal DiscountAmount { get; set; }
}

