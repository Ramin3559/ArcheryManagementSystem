namespace EShooting.Application.Common;

public static class GymLaneRules
{
    public const int LaneNumber = 12;

    public static bool IsGymLane(int laneNumber) => laneNumber == LaneNumber;
}
