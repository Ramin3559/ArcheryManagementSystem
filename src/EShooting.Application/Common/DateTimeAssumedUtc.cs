namespace EShooting.Application.Common;

/// <summary>
/// SQL/EF çox vaxt <see cref="DateTimeKind.Unspecified"/> qaytarır; layihədə vaxtlar UTC kimi saxlanılır.
/// </summary>
public static class DateTimeAssumedUtc
{
    public static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
