namespace EShooting.Web.Contracts.Equipment;

public sealed class EquipmentItemFormModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int RentalQuantity { get; set; }
    public int SaleQuantity { get; set; }
    public int DamagedQuantity { get; set; }
    public decimal? Price { get; set; }
}

public sealed class EquipmentHistoryFilter
{
    public DateTime? FromLocal { get; set; }
    public DateTime? ToLocal { get; set; }
    public Guid? EquipmentItemId { get; set; }
    public EShooting.Domain.Enums.EquipmentIssueType? IssueType { get; set; }
    public Guid? IssuedByStaffId { get; set; }
}
