using EShooting.Application.Common.Models;
using EShooting.Domain.Enums;

namespace EShooting.Web.Helpers;

public static class EquipmentDisplayHelper
{
    public static string FormatPrice(decimal? price)
        => price is > 0 ? $"{price.Value:0.##} AZN" : "—";

    public static string FormatUnitPrice(EquipmentCatalogItem item)
        => FormatPrice(item.UnitPrice ?? item.Price);

    public static string FormatCategory(string? category)
        => string.IsNullOrWhiteSpace(category) ? "—" : category;

    public static string FormatUsageMode(EquipmentUsageMode mode) => mode switch
    {
        EquipmentUsageMode.Sale => "Satış",
        EquipmentUsageMode.Rental => "İcarə (zal)",
        EquipmentUsageMode.Both => "Hər ikisi",
        _ => mode.ToString()
    };
}
