namespace EShooting.Web.Helpers;

public static class EquipmentDisplayHelper
{
    public static string FormatPrice(decimal? price)
        => price is > 0 ? $"{price.Value:0.##} AZN" : "—";

    public static string FormatCategory(string? category)
        => string.IsNullOrWhiteSpace(category) ? "—" : category;
}
