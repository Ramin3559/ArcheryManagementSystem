namespace EShooting.Web.Contracts.Subscriptions;

public sealed class ExcludeSubscriptionOccurrenceRequest
{
    public string DateLocal { get; set; } = "";
}

public sealed class UpsertSubscriptionOccurrenceOverrideRequest
{
    public string DateLocal { get; set; } = "";
    public string StartTimeLocal { get; set; } = "";
    public int LaneNumber { get; set; }
    public int? DurationMinutes { get; set; }
}

public sealed class RescheduleSubscriptionOccurrenceRequest
{
    public string SourceDateLocal { get; set; } = "";
    public string TargetDateLocal { get; set; } = "";
    public string StartTimeLocal { get; set; } = "";
    public int LaneNumber { get; set; }
    public int? DurationMinutes { get; set; }
}
