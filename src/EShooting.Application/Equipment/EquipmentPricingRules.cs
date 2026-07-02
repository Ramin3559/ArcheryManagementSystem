using EShooting.Domain.Entities;
using EShooting.Domain.Enums;

namespace EShooting.Application.Equipment;

/// <summary>Avadanlıq kataloqunda Price = 1 ədəd satış qiyməti (vahid).</summary>
public static class EquipmentPricingRules
{
    public static decimal ResolveUnitPrice(EquipmentItem item, EquipmentIssueType issueType = EquipmentIssueType.Sale)
    {
        if (issueType == EquipmentIssueType.Rental)
        {
            return 0m;
        }

        return Math.Max(0m, item.Price ?? 0m);
    }
}
