namespace EShooting.Web.Contracts.Sessions;

/// <summary>
/// Sessiya planlamasi ucun request modeli.
/// </summary>
public sealed class ScheduleSessionRequest
{
    /// <summary>
    /// Planlanacaq idmancinin identifikatoru.
    /// </summary>
    public Guid AthleteId { get; set; }

    /// <summary>
    /// Secilen lane nomresi.
    /// </summary>
    public int LaneNumber { get; set; }

    /// <summary>
    /// Lane auto secilende (LaneNumber=0) prefer olunan xett tipi.
    /// </summary>
    public EShooting.Domain.Enums.PreferredLaneType PreferredLaneType { get; set; } = EShooting.Domain.Enums.PreferredLaneType.Any;

    /// <summary>
    /// Sessiyanin UTC formatinda baslama vaxti.
    /// </summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// Sessiyanin deqiqe ile muddeti.
    /// </summary>
    public int DurationMinutes { get; set; } = 90;

    /// <summary>
    /// VIP / limitsiz sessiya — bitmə vaxtı yoxdur (resepsiya stop edənə qədər).
    /// </summary>
    public bool ForceOpenEnded { get; set; }

    public Guid? ServicePackageId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AmountPaidCash { get; set; }
    public decimal AmountPaidCard { get; set; }
    public bool IsComplimentary { get; set; }

    public bool IsEquipmentIssued { get; set; }

    public List<SessionEquipmentIssueDto> EquipmentIssues { get; set; } = [];
}
