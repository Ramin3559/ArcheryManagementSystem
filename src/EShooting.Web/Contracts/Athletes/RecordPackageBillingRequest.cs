namespace EShooting.Web.Contracts.Athletes;

public sealed class RecordPackageBillingRequest
{
    public Guid? ServicePackageId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }
    public bool IsComplimentary { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? SubscriptionScheduleId { get; set; }
}
