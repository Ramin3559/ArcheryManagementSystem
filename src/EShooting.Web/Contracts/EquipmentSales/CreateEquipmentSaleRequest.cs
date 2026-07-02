namespace EShooting.Web.Contracts.EquipmentSales;

public sealed class CreateEquipmentSaleRequest
{
    public Guid? AthleteId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }

    public IReadOnlyList<EquipmentSaleLineRequest> Lines { get; set; } = [];
}

