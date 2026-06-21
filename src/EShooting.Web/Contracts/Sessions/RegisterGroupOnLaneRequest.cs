namespace EShooting.Web.Contracts.Sessions;

public sealed class RegisterGroupOnLaneRequest
{
    public List<string> AthleteNames { get; set; } = [];
    public int LaneNumber { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public int DurationMinutes { get; set; } = 90;
    public bool IsEquipmentIssued { get; set; }
}
