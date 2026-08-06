namespace EShooting.Web.Contracts.Sessions;

public sealed class MoveSessionLaneRequest
{
    public int LaneNumber { get; set; }
    public bool AllowSwap { get; set; }
    public bool AllowAmateurOnProLane { get; set; }
}
