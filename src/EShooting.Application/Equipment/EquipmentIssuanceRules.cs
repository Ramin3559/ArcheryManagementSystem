using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Equipment;

public static class EquipmentIssuanceRules
{
    public static EquipmentUsageMode DeriveUsageMode(int rentalQuantity, int saleQuantity)
    {
        var hasRental = rentalQuantity > 0;
        var hasSale = saleQuantity > 0;
        if (hasRental && hasSale) return EquipmentUsageMode.Both;
        if (hasRental) return EquipmentUsageMode.Rental;
        if (hasSale) return EquipmentUsageMode.Sale;
        return EquipmentUsageMode.Both;
    }

    public static void SyncDerivedFields(EquipmentItem item)
    {
        item.Quantity = Math.Max(0, item.RentalQuantity) + Math.Max(0, item.SaleQuantity);
        item.UsageMode = DeriveUsageMode(item.RentalQuantity, item.SaleQuantity);
        item.UpdatedAtUtc = DateTime.UtcNow;
    }

    public static void ValidateIssueType(EquipmentItem item, EquipmentIssueType issueType)
    {
        if (issueType == EquipmentIssueType.Sale && item.SaleQuantity <= 0)
        {
            throw new InvalidOperationException($"«{item.Name}» satış üçün stokda yoxdur.");
        }

        if (issueType == EquipmentIssueType.Rental && item.RentalQuantity <= 0)
        {
            throw new InvalidOperationException($"«{item.Name}» zal icarəsi üçün stokda yoxdur.");
        }
    }

    public static decimal ResolveUnitPrice(EquipmentItem item, EquipmentIssueType issueType = EquipmentIssueType.Sale) =>
        EquipmentPricingRules.ResolveUnitPrice(item, issueType);

    public static void EnsureStockAvailable(EquipmentItem item, EquipmentIssueType issueType, int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Avadanlıq sayı 0-dan böyük olmalıdır.");
        }

        var available = issueType == EquipmentIssueType.Sale ? item.SaleQuantity : item.RentalQuantity;
        if (available < quantity)
        {
            var pool = issueType == EquipmentIssueType.Sale ? "satış" : "zal icarə";
            throw new InvalidOperationException(
                $"«{item.Name}» üçün {pool} stoku kifayət etmir (mövcud: {available}, istənilən: {quantity}).");
        }
    }

    public static void ApplyStockOnIssue(EquipmentItem item, EquipmentIssueType issueType, int quantity)
    {
        EnsureStockAvailable(item, issueType, quantity);
        if (issueType == EquipmentIssueType.Sale)
        {
            item.SaleQuantity -= quantity;
        }
        else
        {
            item.RentalQuantity -= quantity;
        }

        SyncDerivedFields(item);
    }

    public static void ApplyStockOnReturn(EquipmentItem item, EquipmentIssueType issueType, int quantity)
    {
        if (quantity <= 0) return;
        if (issueType == EquipmentIssueType.Sale)
        {
            item.SaleQuantity += quantity;
        }
        else
        {
            item.RentalQuantity += quantity;
        }

        SyncDerivedFields(item);
    }

    public static void ApplyDamagedOnReturn(EquipmentItem item, int quantity)
    {
        if (quantity <= 0) return;
        item.DamagedQuantity += quantity;
        item.UpdatedAtUtc = DateTime.UtcNow;
    }
}
