using System;

namespace EShooting.Web.Contracts.Subscriptions;

public sealed class CancelSubscriptionRequest
{
    public Guid AthleteId { get; set; }
}

