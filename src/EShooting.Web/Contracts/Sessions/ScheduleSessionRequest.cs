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
    public int DurationMinutes { get; set; } = 60;

    public bool IsEquipmentIssued { get; set; }

    public List<SessionEquipmentIssueDto> EquipmentIssues { get; set; } = [];
}
