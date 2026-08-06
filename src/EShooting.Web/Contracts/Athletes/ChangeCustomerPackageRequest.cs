namespace EShooting.Web.Contracts.Athletes;

public sealed class ChangeCustomerPackageRequest
{
    public Guid NewServicePackageId { get; set; }
    public DateTime PeriodStartLocal { get; set; }
    public DateTime PeriodEndLocal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }
    public bool IsComplimentary { get; set; }
    public bool ConfirmDifference { get; set; }
}
