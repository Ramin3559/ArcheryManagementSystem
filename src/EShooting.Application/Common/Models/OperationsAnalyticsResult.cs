namespace EShooting.Application.Common.Models;

public sealed class OperationsAnalyticsResult
{
    public string Mode { get; init; } = "today";
    public string FromLocal { get; init; } = "";
    public string ToLocal { get; init; } = "";
    public string Label { get; init; } = "";

    public int SessionCount { get; init; }
    public int UniqueCustomerCount { get; init; }
    public int NewCustomerCount { get; init; }
    public int SubscriptionCreatedCount { get; init; }

    public int EquipmentSaleCount { get; init; }
    public int EquipmentRentalCount { get; init; }
    public decimal EquipmentSaleRevenue { get; init; }
    public decimal EquipmentRentalRevenue { get; init; }

    public double TotalLaneHours { get; init; }
    public int? BusiestLaneNumber { get; init; }

    public IReadOnlyCollection<DailyOperationsRow> DailyBreakdown { get; init; } = [];
    public IReadOnlyCollection<LaneActivityRow> LaneActivity { get; init; } = [];
    public IReadOnlyCollection<EquipmentAnalyticsRow> EquipmentBreakdown { get; init; } = [];
}

public sealed class DailyOperationsRow
{
    public string DateLocal { get; init; } = "";
    public int SessionCount { get; init; }
    public int UniqueCustomerCount { get; init; }
    public int NewCustomerCount { get; init; }
    public int SubscriptionCreatedCount { get; init; }
    public int EquipmentSaleCount { get; init; }
    public int EquipmentRentalCount { get; init; }
    public decimal EquipmentSaleRevenue { get; init; }
    public decimal EquipmentRentalRevenue { get; init; }
    public double LaneHoursTotal { get; init; }
}

public sealed class LaneActivityRow
{
    public int LaneNumber { get; init; }
    public int SessionCount { get; init; }
    public double TotalHours { get; init; }
}

public sealed class EquipmentAnalyticsRow
{
    public string EquipmentName { get; init; } = "";
    public int SaleCount { get; init; }
    public int RentalCount { get; init; }
    public decimal SaleRevenue { get; init; }
    public decimal RentalRevenue { get; init; }
}
