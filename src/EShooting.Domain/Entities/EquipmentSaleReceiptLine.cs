namespace EShooting.Domain.Entities;

public sealed class EquipmentSaleReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    /// <summary>Bu sətir üzrə endirim (AZN).</summary>
    public decimal DiscountAmount { get; set; }
}

