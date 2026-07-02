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
    public int EquipmentRentalIssuedCount { get; init; }
    public int EquipmentRentalReturnedCount { get; init; }
    public int EquipmentRentalOutstandingCount { get; init; }
    public decimal EquipmentSaleRevenue { get; init; }

    public double TotalLaneHours { get; init; }
    public int? BusiestLaneNumber { get; init; }

    public int PackageRecordCount { get; init; }
    public int ComplimentaryCount { get; init; }
    public decimal PackagePriceDue { get; init; }
    public decimal PackagePaidCash { get; init; }
    public decimal PackagePaidCard { get; init; }
    public decimal PackagePaidTotal { get; init; }
    public decimal PackageRemaining { get; init; }

    public int StandaloneEquipmentSaleCount { get; init; }
    public decimal StandaloneEquipmentSaleDue { get; init; }
    public decimal StandaloneEquipmentPaidCash { get; init; }
    public decimal StandaloneEquipmentPaidCard { get; init; }
    public decimal StandaloneEquipmentPaidTotal { get; init; }
    public decimal StandaloneEquipmentRemaining { get; init; }

    public decimal TotalPriceDue { get; init; }
    public decimal TotalPaidCash { get; init; }
    public decimal TotalPaidCard { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal TotalRemaining { get; init; }

    public IReadOnlyCollection<DailyOperationsRow> DailyBreakdown { get; init; } = [];
    public DailyBreakdownTotals DailyTotals { get; init; } = new();
    public IReadOnlyCollection<LaneActivityRow> LaneActivity { get; init; } = [];
    public IReadOnlyCollection<EquipmentSaleDetailRow> EquipmentSaleDetails { get; init; } = [];
    public IReadOnlyCollection<CustomerVisitDetailRow> CustomerVisitDetails { get; init; } = [];
}

public sealed class DailyOperationsRow
{
    public string DateLocal { get; init; } = "";
    public int SessionCount { get; init; }
    public int UniqueCustomerCount { get; init; }
    public int NewCustomerCount { get; init; }
    public int SubscriptionCreatedCount { get; init; }
    public int EquipmentSaleCount { get; init; }
    public int EquipmentRentalIssuedCount { get; init; }
    public int EquipmentRentalReturnedCount { get; init; }
    public decimal EquipmentSaleRevenue { get; init; }
    public double LaneHoursTotal { get; init; }

    public int PackageRecordCount { get; init; }
    public int ComplimentaryCount { get; init; }
    public decimal PackagePriceDue { get; init; }
    public decimal PackagePaidCash { get; init; }
    public decimal PackagePaidCard { get; init; }
    public decimal PackagePaidTotal { get; init; }
    public decimal PackageRemaining { get; init; }

    public int StandaloneEquipmentSaleCount { get; init; }
    public decimal StandaloneEquipmentSaleDue { get; init; }
    public decimal StandaloneEquipmentPaidCash { get; init; }
    public decimal StandaloneEquipmentPaidCard { get; init; }
    public decimal StandaloneEquipmentPaidTotal { get; init; }
    public decimal StandaloneEquipmentRemaining { get; init; }

    public decimal TotalPriceDue { get; init; }
    public decimal TotalPaidCash { get; init; }
    public decimal TotalPaidCard { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal TotalRemaining { get; init; }
}

public sealed class DailyBreakdownTotals
{
    public int SessionCount { get; init; }
    public int UniqueCustomerCount { get; init; }
    public int NewCustomerCount { get; init; }
    public int SubscriptionCreatedCount { get; init; }
    public int EquipmentSaleCount { get; init; }
    public int EquipmentRentalIssuedCount { get; init; }
    public int EquipmentRentalReturnedCount { get; init; }
    public decimal EquipmentSaleRevenue { get; init; }
    public double LaneHoursTotal { get; init; }

    public int PackageRecordCount { get; init; }
    public int ComplimentaryCount { get; init; }
    public decimal PackagePriceDue { get; init; }
    public decimal PackagePaidCash { get; init; }
    public decimal PackagePaidCard { get; init; }
    public decimal PackagePaidTotal { get; init; }
    public decimal PackageRemaining { get; init; }

    public int StandaloneEquipmentSaleCount { get; init; }
    public decimal StandaloneEquipmentSaleDue { get; init; }
    public decimal StandaloneEquipmentPaidCash { get; init; }
    public decimal StandaloneEquipmentPaidCard { get; init; }
    public decimal StandaloneEquipmentPaidTotal { get; init; }
    public decimal StandaloneEquipmentRemaining { get; init; }

    public decimal TotalPriceDue { get; init; }
    public decimal TotalPaidCash { get; init; }
    public decimal TotalPaidCard { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal TotalRemaining { get; init; }
}

public sealed class LaneActivityRow
{
    public int LaneNumber { get; init; }
    public int SessionCount { get; init; }
    public double TotalHours { get; init; }
}

public sealed class EquipmentSaleDetailRow
{
    public string DateLocal { get; init; } = "";
    public string TimeLocal { get; init; } = "";
    public string EquipmentName { get; init; } = "";
    /// <summary>Cari fiziki stok (anbarda + zaldə).</summary>
    public int TotalQuantity { get; init; }
    /// <summary>Hazırda zaldə / müştəridə (icarə).</summary>
    public int InHallQuantity { get; init; }
    /// <summary>Anbarda satış üçün mövcud.</summary>
    public int ForSaleQuantity { get; init; }
    public int SoldQuantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public decimal PaidCash { get; init; }
    public decimal PaidCard { get; init; }
    public decimal DiscountAmount { get; init; }
    public string CustomerName { get; init; } = "";
    public string SoldByStaffName { get; init; } = "";
    public string SaleSource { get; init; } = "";
}

public sealed class CustomerVisitDetailRow
{
    public string DateLocal { get; init; } = "";
    public string CustomerName { get; init; } = "";
    public string Phone { get; init; } = "";
    /// <summary>Resepsiyada zolağa yazılışı edən işçi (qeydiyyatçı).</summary>
    public string ReceptionStaffName { get; init; } = "";
    /// <summary>Planşetdə (zalda) nəzarət edən işçi.</summary>
    public string SupervisorStaffName { get; init; } = "";
    public string PackageName { get; init; } = "";
    public string RecordedAtLocal { get; init; } = "";
    public int? LaneNumber { get; init; }
    public string StartTimeLocal { get; init; } = "";
    public string EndTimeLocal { get; init; } = "";
    public double DurationHours { get; init; }
    public string DurationLabel { get; init; } = "";
    public decimal PriceDue { get; init; }
    public decimal AmountPaidCash { get; init; }
    public decimal AmountPaidCard { get; init; }
    public decimal AmountPaid { get; init; }
    public decimal DiscountAmount { get; init; }
    public bool IsComplimentary { get; init; }
}
