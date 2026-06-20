namespace EShooting.Web.Contracts.Equipment;

public sealed class EquipmentItemFormModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; }
    public decimal? Price { get; set; }
    public bool IsActive { get; set; } = true;
}
