namespace EShooting.Application.Common;

/// <summary>
/// Abunə həftə günü + saat üçün ilk keçərli tarix (cari vaxtdan geriyə düşməsin).
/// </summary>
public static class SubscriptionOccurrenceRules
{
    /// <summary>
    /// ActiveFrom-dan başlayaraq DayOfWeek uyğun ilk gün.
    /// Həmin günün StartTimeLocal-i artıq keçibsə → +7 gün (növbəti həftə eyni gün).
    /// </summary>
    public static DateTime ResolveFirstOccurrenceDateLocal(
        DateTime activeFromDateLocal,
        int dayOfWeek,
        TimeSpan startTimeLocal,
        DateTime? nowLocal = null)
    {
        var now = nowLocal ?? AzerbaijanTime.NowLocal;
        var cursor = activeFromDateLocal.Date;

        for (var guard = 0; guard < 14 && (int)cursor.DayOfWeek != dayOfWeek; guard++)
        {
            cursor = cursor.AddDays(1);
        }

        var slotLocal = cursor.Add(startTimeLocal);
        if (slotLocal <= now)
        {
            cursor = cursor.AddDays(7);
        }

        return cursor;
    }

    /// <summary>Bu gün + saat artıq keçibsə true.</summary>
    public static bool IsSlotInThePast(DateTime dayLocal, TimeSpan startTimeLocal, DateTime? nowLocal = null)
    {
        var now = nowLocal ?? AzerbaijanTime.NowLocal;
        return dayLocal.Date.Add(startTimeLocal) <= now;
    }
}
