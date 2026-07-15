namespace EShooting.Web.Contracts.Sessions;

public sealed class RegisterGroupOnLaneRequest
{
    public List<string> AthleteNames { get; set; } = [];
    public int LaneNumber { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public int DurationMinutes { get; set; } = 90;
    public bool IsEquipmentIssued { get; set; }
    public bool ActivateImmediately { get; set; }

    public Guid? ServicePackageId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }
    public bool IsComplimentary { get; set; }

    public List<SessionEquipmentIssueDto> EquipmentIssues { get; set; } = [];
}
