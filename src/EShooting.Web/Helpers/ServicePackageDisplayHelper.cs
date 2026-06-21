using EShooting.Domain.Enums;

namespace EShooting.Web.Helpers;

public static class ServicePackageDisplayHelper
{
    public static string BillingType(PackageBillingType value) => value switch
    {
        PackageBillingType.OneTime => "Birdefəlik",
        PackageBillingType.Monthly => "Aylıq",
        PackageBillingType.Yearly => "İllik",
        PackageBillingType.Vip => "VIP",
        PackageBillingType.Gym => "Zal",
        _ => value.ToString()
    };

    public static string Scope(PackageScope value) => value switch
    {
        PackageScope.Archery => "Yalnız oxatma",
        PackageScope.Gym => "Yalnız zal",
        PackageScope.Full => "Full (oxatma + zal)",
        PackageScope.Vip => "VIP",
        _ => value.ToString()
    };

    public static string SchedulingMode(PackageSchedulingMode value) => value switch
    {
        PackageSchedulingMode.None => "—",
        PackageSchedulingMode.FixedWeekly => "Sabit həftəlik plan",
        PackageSchedulingMode.WalkInFlexible => "Çevik (walk-in)",
        _ => value.ToString()
    };

    public static string FormatMinutes(int? minutes)
    {
        if (minutes is null or <= 0) return "—";
        if (minutes.Value % 60 == 0)
        {
            return $"{minutes.Value / 60} saat";
        }

        return $"{minutes.Value} dəq";
    }

    public static string FormatPrice(decimal price) => $"{price:0.##} AZN";

    public static string WeeklyDaysShort(string? csv, PackageSchedulingMode schedulingMode = PackageSchedulingMode.None)
    {
        if (schedulingMode == PackageSchedulingMode.WalkInFlexible) return "Plan yox";
        if (string.IsNullOrWhiteSpace(csv)) return "—";
        var labels = new Dictionary<int, string>
        {
            [0] = "B.",
            [1] = "B.e",
            [2] = "Ç.a",
            [3] = "Ç",
            [4] = "C.a",
            [5] = "C",
            [6] = "Ş"
        };
        return string.Join(", ", csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.TryParse(p, out var n) && labels.TryGetValue(n, out var l) ? l : p));
    }
}
