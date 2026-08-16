namespace EShooting.Application.Common;



/// <summary>

/// Aylıq sabit plan: 1 ay = 4 həftə × həftə günü sayı gediş.

/// ActiveTo = N-ci plan günü.

/// Qalıq gediş varsa plan bitəndən sonra +1 ay gəlmə icazəsi (makeup).

/// </summary>

public static class WeeklyVisitPeriodRules

{

    /// <summary>Plan bitdikdən sonra qalıq gedişləri götürmək üçün icazə müddəti.</summary>

    public const int MakeupBonusMonths = 1;



    public static int VisitLimit(int weeklyDaysCount, int months) =>

        Math.Max(1, weeklyDaysCount) * 4 * Math.Max(1, months);



    /// <summary>

    /// Başlanğıc tarixdən (daxil) yalnız seçilmiş həftə günlərini sayaraq N-ci günün tarixi (= ActiveTo).

    /// </summary>

    public static DateTime ComputeEndDate(

        DateTime startLocal,

        IReadOnlyList<int> weekdaysOfWeek,

        int months)

    {

        var days = weekdaysOfWeek

            .Where(d => d is >= 0 and <= 6)

            .Distinct()

            .ToList();

        if (days.Count == 0)

        {

            throw new InvalidOperationException("Həftə günləri seçilməyib.");

        }



        var visitCount = VisitLimit(days.Count, months);

        var set = days.ToHashSet();

        var cursor = startLocal.Date;

        var found = 0;

        for (var i = 0; i < 800; i++)

        {

            if (set.Contains((int)cursor.DayOfWeek))

            {

                found++;

                if (found >= visitCount)

                {

                    return cursor;

                }

            }



            cursor = cursor.AddDays(1);

        }



        throw new InvalidOperationException("Abunə bitmə tarixi hesablana bilmədi.");

    }



    public static DateTime MakeupDeadline(DateTime activeToLocal) =>

        activeToLocal.Date.AddMonths(MakeupBonusMonths);



    /// <summary>

    /// Plan daxilində və ya qalıq gediş makeup pəncərəsindədir?

    /// </summary>

    public static bool IsWithinAccessWindow(

        DateTime dayLocal,

        DateTime activeFromLocal,

        DateTime activeToLocal,

        int remainingVisits)

    {

        var day = dayLocal.Date;

        var from = activeFromLocal.Date;

        var to = activeToLocal.Date;

        if (day < from)

        {

            return false;

        }



        if (day <= to)

        {

            return true;

        }



        return remainingVisits > 0 && day <= MakeupDeadline(to);

    }



    /// <summary>Gediş limiti = dövrdə planlaşdırılan gün sayı (ActiveTo = plan sonu).</summary>

    public static int ResolveVisitLimit(

        IEnumerable<(int DayOfWeek, IReadOnlySet<string> ExcludedDateKeys)> schedules,

        DateTime periodFrom,

        DateTime periodTo,

        int weeklyDaysCount)

    {

        var planned = CountPlannedOccurrences(schedules, periodFrom, periodTo);

        if (planned > 0)

        {

            return planned;

        }



        return VisitLimit(Math.Max(1, weeklyDaysCount), 1);

    }



    /// <summary>

    /// Dövrdə planlaşdırılan unikal gün sayı (gediş limiti).

    /// </summary>

    public static int CountPlannedOccurrences(

        IEnumerable<(int DayOfWeek, IReadOnlySet<string> ExcludedDateKeys)> schedules,

        DateTime periodFrom,

        DateTime periodTo)

    {

        var from = periodFrom.Date;

        var to = periodTo.Date;

        if (to < from)

        {

            return 0;

        }



        var dates = new HashSet<DateTime>();

        var list = schedules.ToList();

        for (var day = from; day <= to; day = day.AddDays(1))

        {

            var dow = (int)day.DayOfWeek;

            var key = day.ToString("yyyy-MM-dd");

            foreach (var s in list)

            {

                if (s.DayOfWeek != dow)

                {

                    continue;

                }



                if (s.ExcludedDateKeys.Contains(key))

                {

                    continue;

                }



                dates.Add(day);

                break;

            }

        }



        return dates.Count;

    }

}


