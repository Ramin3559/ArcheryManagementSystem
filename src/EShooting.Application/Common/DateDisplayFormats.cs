namespace EShooting.Application.Common;

/// <summary>İstifadəçiyə göstərilən tarix formatı: gün.ay.il</summary>
public static class DateDisplayFormats
{
    public const string Date = "dd.MM.yyyy";
    public const string DateTime = "dd.MM.yyyy HH:mm";

    public static string FormatDate(DateTime value) => value.ToString(Date);

    public static string FormatDateTime(DateTime value) => value.ToString(DateTime);
}
