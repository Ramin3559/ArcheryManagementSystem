using EShooting.Domain.Enums;

namespace EShooting.Application.Common.Models;

public sealed class CustomerListItem
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public string? Email { get; init; }
    public string? IdCardNumber { get; init; }
    public string? ClubCardNumber { get; init; }
    public CustomerCategory Category { get; init; }
    public string CategoryLabel { get; init; } = "";
    public bool IsVip { get; init; }
    public bool IsActive { get; init; }
    public bool IsSubscriber { get; init; }
    public string PackageTypeLabel { get; init; } = "";
    public string? SubscriptionFromLocal { get; init; }
    public string? SubscriptionToLocal { get; init; }
    public string RegisteredAtLocal { get; init; } = "";
    public string RegisteredByStaffName { get; init; } = "—";
    public string? DeletedAtLocal { get; init; }
    public string DeletedByName { get; init; } = "—";
    /// <summary>Zolağa heç vaxt yazılıbmı.</summary>
    public bool HasLaneHistory { get; init; }
    /// <summary>Resepsiyadan avadanlıq satışı (EquipmentSaleReceipt).</summary>
    public bool HasStandaloneEquipmentPurchase { get; init; }
    public string CustomerTypeLabel { get; init; } = "Müştəri";
    /// <summary>Oyun müddətində sessiya avadanlığı verilib.</summary>
    public bool HasSessionEquipmentRental { get; init; }
    public bool HasPendingSessionRental { get; init; }
    public bool HasEquipmentHistory { get; init; }
    public bool HasPendingEquipment { get; init; }
    /// <summary>Son zolağa yazılma tarixi (Bakı vaxtı).</summary>
    public string? LastLaneVisitLocal { get; init; }
    public int? LastLaneNumber { get; init; }
    public string? LastVisitLocal { get; init; }
    public int? ActiveLaneNumber { get; init; }
    public string? CurrentPackageName { get; init; }
    /// <summary>Son paket ödənişinin ümumi məbləği (AZN).</summary>
    public decimal? LatestAmountPaid { get; init; }
    public decimal? LatestAmountPaidCash { get; init; }
    public decimal? LatestAmountPaidCard { get; init; }
    public bool LatestPaymentIsComplimentary { get; init; }
    public string LatestPaymentLabel { get; init; } = "—";
    public Guid? LatestPackageRecordId { get; init; }
    public bool HasPackagePayments { get; init; }
}

public sealed class CustomersListResult
{
    public IReadOnlyCollection<CustomerListItem> Items { get; init; } = [];
    public int TotalCount { get; init; }
}
