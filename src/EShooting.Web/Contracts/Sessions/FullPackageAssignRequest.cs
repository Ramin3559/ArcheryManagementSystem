namespace EShooting.Web.Contracts.Sessions;

public sealed class FullPackageAssignRequest
{
    public Guid AthleteId { get; set; }
    public int DurationMinutes { get; set; } = 90;
    public bool IsEquipmentIssued { get; set; }
}

