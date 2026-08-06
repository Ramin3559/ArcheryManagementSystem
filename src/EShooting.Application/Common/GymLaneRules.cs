namespace EShooting.Application.Common;

public static class GymLaneRules
{
    public const int LaneNumber = 12;

    public static bool IsGymLane(int laneNumber) => laneNumber == LaneNumber;

    /// <summary>0 = pool/seçilməyib; 1–11 oxatma; 12 = Trenajor.</summary>
    public static bool IsValidScheduleLaneNumber(int laneNumber)
        => laneNumber is >= 0 and <= 11 || IsGymLane(laneNumber);
}
