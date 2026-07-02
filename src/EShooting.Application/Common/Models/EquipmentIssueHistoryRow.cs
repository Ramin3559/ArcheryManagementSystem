using EShooting.Domain.Enums;

namespace EShooting.Application.Common.Models;

public sealed class EquipmentIssueHistoryRow
{
    public Guid IssueId { get; init; }
    public string IssuedAtLocal { get; init; } = string.Empty;
    public string EquipmentName { get; init; } = string.Empty;
    public string? Category { get; init; }
    public EquipmentIssueType IssueType { get; init; }
    public string IssueTypeLabel { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public int? LaneNumber { get; init; }
    public string IssuedByStaffName { get; init; } = string.Empty;
    public string? ReturnedAtLocal { get; init; }
    public string? ReturnedByStaffName { get; init; }
}

public sealed class EquipmentIssueHistoryResult
{
    public IReadOnlyCollection<EquipmentIssueHistoryRow> Items { get; init; } = [];
    public int SaleQuantityTotal { get; init; }
    public int RentalQuantityTotal { get; init; }
    public decimal SaleRevenueTotal { get; init; }
    public decimal RentalRevenueTotal { get; init; }
    public decimal GrandTotal { get; init; }
}
